#![allow(clippy::print_stderr)]
#![allow(clippy::print_stdout)]

use flate2::write::GzEncoder;
use log::{LevelFilter, Log, Record};
#[cfg(feature = "console")]
use rustyline_async::Readline;
use simplelog::{CombinedLogger, Config, SharedLogger, WriteLogger};
use std::fmt::format;
use std::fs::File;
use std::io::{self, BufWriter};
use std::path::PathBuf;
use time::{Duration, OffsetDateTime, UtcOffset};

const MAX_ATTEMPTS: u32 = 100;

/// A wrapper for our logger to hold the terminal input while no input is expected in order to
/// properly flush logs to the output while they happen instead of batched
pub struct ReadlineLogWrapper {
    internal: Box<CombinedLogger>,
    #[cfg(feature = "console")]
    readline: std::sync::Mutex<Option<rustyline_async::Readline>>,
}

struct GzipRollingLoggerData {
    pub current_day_of_month: u8,
    pub last_rotate_time: time::OffsetDateTime,
    pub latest_logger: WriteLogger<File>,
    pub latest_filename: String,
    pub log_dir: PathBuf,
}

pub struct GzipRollingLogger {
    log_level: LevelFilter,
    data: std::sync::Mutex<GzipRollingLoggerData>,
    config: Config,
}

impl GzipRollingLogger {
    pub fn new(
        log_level: LevelFilter,
        config: Config,
        filename: String,
        base_path: &std::path::Path,
        external_callback: Option<LogCallbackFn>, // Pass callback for early reporting
    ) -> Result<Box<Self>, Box<dyn std::error::Error>> {
        let now = time::OffsetDateTime::now_utc();

        // UWP SHIM: Force absolute path inside probe_root
        let log_dir = base_path.join("logs");
        std::fs::create_dir_all(&log_dir)?;

        let latest_path = log_dir.join(&filename);

        if latest_path.exists() {
            let msg = format!(
                "Found existing log file at '{}', gzipping it now...",
                latest_path.display()
            );

            // Report to UWP UI via callback
            if let Some(cb) = external_callback {
                if let Ok(c_str) = std::ffi::CString::new(msg) {
                    unsafe {
                        (cb)(c_str.as_ptr());
                    }
                }
            } else {
                eprintln!("{}", msg);
            }

            let new_gz_path = Self::new_filename(true, &log_dir)?;

            let mut file = File::open(&latest_path)?;
            let mut encoder = GzEncoder::new(
                BufWriter::new(File::create(&new_gz_path)?),
                flate2::Compression::best(),
            );

            io::copy(&mut file, &mut encoder)?;
            encoder.finish()?;

            std::fs::remove_file(&latest_path)?;
        }

        let new_logger = WriteLogger::new(log_level, config.clone(), File::create(&latest_path)?);

        Ok(Box::new(Self {
            log_level,
            data: std::sync::Mutex::new(GzipRollingLoggerData {
                current_day_of_month: now.day(),
                last_rotate_time: now,
                latest_filename: filename,
                latest_logger: *new_logger,
                log_dir, // Store absolute path for rotations
            }),
            config,
        }))
    }

    pub fn new_filename(
        yesterday: bool,
        log_dir: &std::path::Path,
    ) -> Result<PathBuf, Box<dyn std::error::Error>> {
        let local_offset = UtcOffset::current_local_offset().unwrap_or(UtcOffset::UTC);
        let mut now = OffsetDateTime::now_utc().to_offset(local_offset);

        if yesterday {
            now -= Duration::days(1);
        }

        let date_format = format!("{}-{:02}-{:02}", now.year(), now.month() as u8, now.day());

        for id in 1..=MAX_ATTEMPTS {
            let filename = log_dir.join(format!("{date_format}-{id}.log.gz"));
            if !filename.exists() {
                return Ok(filename);
            }
        }

        Err(format!(
            "Failed to find a unique log filename for date {date_format} after {MAX_ATTEMPTS} attempts.",
        ).into())
    }

    fn rotate_log(&self) -> Result<(), Box<dyn std::error::Error>> {
        let now = time::OffsetDateTime::now_utc();
        let mut data = self.data.lock().unwrap();

        // Use the stored absolute log_dir
        let new_gz_path = Self::new_filename(true, &data.log_dir)?;
        let latest_path = data.log_dir.join(&data.latest_filename);

        let mut file = File::open(&latest_path)?;
        let mut encoder = GzEncoder::new(
            BufWriter::new(File::create(&new_gz_path)?),
            flate2::Compression::best(),
        );
        io::copy(&mut file, &mut encoder)?;
        encoder.finish()?;

        data.current_day_of_month = now.day();
        data.last_rotate_time = now;
        data.latest_logger = *WriteLogger::new(
            self.log_level,
            self.config.clone(),
            File::create(&latest_path)?,
        );
        Ok(())
    }
}

fn remove_ansi_color_code(s: &str) -> String {
    let mut result = String::with_capacity(s.len());
    let mut it = s.chars();

    while let Some(c) = it.next() {
        if c == '\x1b' {
            for c_seq in it.by_ref() {
                if c_seq.is_ascii_alphabetic() {
                    break;
                }
            }
        } else {
            result.push(c);
        }
    }
    result
}

impl Log for GzipRollingLogger {
    fn enabled(&self, metadata: &log::Metadata) -> bool {
        metadata.level() <= self.log_level
    }

    fn log(&self, record: &Record) {
        if !self.enabled(record.metadata()) {
            return;
        }

        let now = time::OffsetDateTime::now_utc();

        if let Ok(data) = self.data.lock() {
            let original_string = format(*record.args());
            let string = remove_ansi_color_code(&original_string);
            data.latest_logger.log(
                &Record::builder()
                    .args(format_args!("{string}"))
                    .metadata(record.metadata().clone())
                    .module_path(record.module_path())
                    .file(record.file())
                    .line(record.line())
                    .build(),
            );
            if data.current_day_of_month != now.day() {
                drop(data);
                if let Err(e) = self.rotate_log() {
                    eprintln!("Failed to rotate log: {e}");
                }
            }
        }
    }

    fn flush(&self) {
        if let Ok(data) = self.data.lock() {
            data.latest_logger.flush();
        }
    }
}

impl SharedLogger for GzipRollingLogger {
    fn level(&self) -> LevelFilter {
        self.log_level
    }

    fn config(&self) -> Option<&Config> {
        Some(&self.config)
    }

    fn as_log(self: Box<Self>) -> Box<dyn Log> {
        Box::new(*self)
    }
}

impl ReadlineLogWrapper {
    #[must_use]
    pub fn new(
        log: Box<dyn SharedLogger + 'static>,
        file_logger: Option<Box<dyn SharedLogger + 'static>>,
        #[cfg(feature = "console")] rl: Option<rustyline_async::Readline>,
    ) -> Self {
        let loggers: Vec<Option<Box<dyn SharedLogger + 'static>>> = vec![Some(log), file_logger];
        Self {
            internal: CombinedLogger::new(loggers.into_iter().flatten().collect()),
            #[cfg(feature = "console")]
            readline: std::sync::Mutex::new(rl),
        }
    }

    #[cfg(feature = "console")]
    pub fn take_readline(&self) -> Option<rustyline_async::Readline> {
        self.readline
            .lock()
            .map_or_else(|_| None, |mut result| result.take())
    }

    // This isn't really dead code, just for some reason rust thinks that it might be.
    // Schroedinger's dead code -> expect warns unfulfilled lint expectation but removing it causes dead_code lint?
    #[allow(dead_code)]
    #[cfg(feature = "console")]
    pub(crate) fn return_readline(&self, rl: rustyline_async::Readline) {
        if let Ok(mut result) = self.readline.lock() {
            let _ = result.insert(rl);
        }
    }

    #[cfg(feature = "console")]
    pub fn new_ffi_compatible(rl: Option<Readline>) -> Self {
        Self {
            internal: CombinedLogger::new(vec![]),
            readline: std::sync::Mutex::new(rl),
        }
    }

    #[cfg(not(feature = "console"))]
    pub fn new_ffi_compatible(_rl: Option<()>) -> Self {
        Self {
            internal: CombinedLogger::new(vec![]),
        }
    }
}

// Writing to `stdout` is expensive anyway, so I don't think having a `Mutex` here is a big deal.
impl Log for ReadlineLogWrapper {
    fn log(&self, record: &log::Record) {
        self.internal.log(record);
        #[cfg(feature = "console")]
        {
            if let Ok(mut lock) = self.readline.lock()
                && let Some(rl) = lock.as_mut()
            {
                let _ = rl.flush();
            }
        }
    }

    fn flush(&self) {
        self.internal.flush();
        #[cfg(feature = "console")]
        {
            if let Ok(mut lock) = self.readline.lock()
                && let Some(rl) = lock.as_mut()
            {
                let _ = rl.flush();
            }
        }
    }

    fn enabled(&self, metadata: &log::Metadata) -> bool {
        self.internal.enabled(metadata)
    }
}

pub type LogCallbackFn = unsafe extern "C" fn(*const std::os::raw::c_char);

pub struct CallbackLogger {
    level: LevelFilter,
    config: Config,
    callback: LogCallbackFn,
}

impl CallbackLogger {
    pub fn new(level: LevelFilter, config: Config, callback: LogCallbackFn) -> Box<Self> {
        Box::new(Self {
            level,
            config,
            callback,
        })
    }
}

impl Log for CallbackLogger {
    fn enabled(&self, metadata: &log::Metadata) -> bool {
        metadata.level() <= self.level
    }

    fn log(&self, record: &Record) {
        if self.enabled(record.metadata()) {
            let msg = format!(
                "[{}] [{}] {}",
                record.level(),
                record.target(),
                record.args()
            );
            if let Ok(c_str) = std::ffi::CString::new(msg) {
                unsafe {
                    (self.callback)(c_str.as_ptr());
                }
            }
        }
    }
    fn flush(&self) {}
}

impl SharedLogger for CallbackLogger {
    fn level(&self) -> LevelFilter {
        self.level
    }
    fn config(&self) -> Option<&Config> {
        Some(&self.config)
    }
    fn as_log(self: Box<Self>) -> Box<dyn Log> {
        Box::new(*self)
    }
}
