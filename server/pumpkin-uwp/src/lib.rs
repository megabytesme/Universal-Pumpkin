use pumpkin::server::Server;
use pumpkin::PumpkinServer;
use pumpkin::entity::player::Player;
use pumpkin::net::ClientPlatform;
use serde::Serialize;
use std::ffi::{CStr, CString};
use std::os::raw::c_char;
use std::sync::{Arc, OnceLock};
use std::sync::atomic::Ordering;
use tokio::runtime::Runtime;
use log::{Record, Metadata, LevelFilter};
use std::sync::atomic::AtomicPtr;

//  GLOBAL STATE
static SERVER_INSTANCE: OnceLock<Arc<Server>> = OnceLock::new();
static RUNTIME: OnceLock<Runtime> = OnceLock::new();
static LOG_CALLBACK: AtomicPtr<()> = AtomicPtr::new(std::ptr::null_mut());

//  DATA MODELS
#[derive(Serialize)]
struct Vector3 {
    x: f64,
    y: f64,
    z: f64,
}

#[derive(Serialize)]
struct SerializedPlayer {
    uuid: String,
    username: String,
    ip_address: String,
    platform: String,
    gamemode: String,
    health: f32,
    food_level: i32,
    saturation: f32,
    exp_level: i32,
    position: Vector3,
    rotation_yaw: f32,
    rotation_pitch: f32,
    dimension: String,
    is_sneaking: bool,
    is_sprinting: bool,
}

impl SerializedPlayer {
    pub fn from_player(player: &Player) -> Self {
        let entity = &player.living_entity.entity;
        let pos = entity.pos.load();
        
        let (ip, platform) = match &player.client {
            ClientPlatform::Java(c) => {
                let addr_str = if let Ok(guard) = c.address.try_lock() {
                    guard.to_string()
                } else {
                    "Locked".to_string()
                };
                (addr_str, "Java".to_string())
            },
            ClientPlatform::Bedrock(c) => (c.address.to_string(), "Bedrock".to_string()),
        };

        Self {
            uuid: player.gameprofile.id.to_string(),
            username: player.gameprofile.name.clone(),
            ip_address: ip,
            platform,
            gamemode: format!("{:?}", player.gamemode.load()),
            health: player.living_entity.health.load(),
            food_level: player.hunger_manager.level.load() as i32,
            saturation: player.hunger_manager.saturation.load(),
            exp_level: player.experience_level.load(Ordering::Relaxed),
            position: Vector3 { x: pos.x, y: pos.y, z: pos.z },
            rotation_yaw: entity.yaw.load(),
            rotation_pitch: entity.pitch.load(),
            dimension: format!("{:?}", entity.world.dimension_type),
            is_sneaking: entity.sneaking.load(Ordering::Relaxed),
            is_sprinting: entity.sprinting.load(Ordering::Relaxed),
        }
    }
}

//  LOGGING BRIDGE
type LogCallback = extern "C" fn(*const c_char);

struct UwpLogger;

impl UwpLogger {
    fn send_to_csharp(msg: &str) {
        let ptr = LOG_CALLBACK.load(Ordering::Relaxed);
        if !ptr.is_null() {
            if let Ok(c_str) = CString::new(msg) {
                unsafe {
                    let cb: LogCallback = std::mem::transmute(ptr);
                    cb(c_str.as_ptr());
                }
            }
        }
    }
}

impl log::Log for UwpLogger {
    fn enabled(&self, metadata: &Metadata) -> bool {
        metadata.level() <= log::Level::Info
    }

    fn log(&self, record: &Record) {
        if self.enabled(record.metadata()) {
            let msg = format!("[{}] [{}] {}", record.level(), record.target(), record.args());
            Self::send_to_csharp(&msg);
        }
    }
    fn flush(&self) {}
}

static UWP_LOGGER: OnceLock<UwpLogger> = OnceLock::new();

fn uwp_init_logger() {
    let logger = UWP_LOGGER.get_or_init(|| UwpLogger);
    let _ = log::set_logger(logger);
    log::set_max_level(LevelFilter::Info);
}

fn uwp_init_panic_hook() {
    std::panic::set_hook(Box::new(|info| {
        let msg = match info.payload().downcast_ref::<&str>() {
            Some(s) => *s,
            None => match info.payload().downcast_ref::<String>() {
                Some(s) => &**s,
                None => "Box<Any>",
            },
        };
        let location = info.location().map(|l| format!("{}:{}:{}", l.file(), l.line(), l.column())).unwrap_or_else(|| "unknown".to_string());
        let err_msg = format!("\n[PANIC] Thread '{:?}' panicked at '{}': {}\n", std::thread::current().name(), location, msg);
        log::error!("{}", err_msg);
        UwpLogger::send_to_csharp(&err_msg);
    }));
}

//  FFI EXPORTS
#[unsafe(no_mangle)]
pub extern "C" fn pumpkin_register_logger(cb: LogCallback) {
    LOG_CALLBACK.store(cb as *mut (), Ordering::Relaxed);
    uwp_init_logger();
    uwp_init_panic_hook();
    log::info!("Callback logger connected successfully.");
}

#[unsafe(no_mangle)]
pub extern "C" fn pumpkin_get_players_json() -> *mut c_char {
    if let Some(server) = SERVER_INSTANCE.get() {
        let mut player_list = Vec::new();
        if let Ok(worlds) = server.worlds.try_read() {
            for world in worlds.iter() {
                if let Ok(players_map) = world.players.try_read() {
                    for (_, player) in players_map.iter() {
                        player_list.push(SerializedPlayer::from_player(player));
                    }
                }
            }
        }
        let json = serde_json::to_string(&player_list).unwrap_or_else(|_| "[]".to_string());
        return CString::new(json).unwrap().into_raw();
    }
    CString::new("[]").unwrap().into_raw()
}

#[unsafe(no_mangle)]
pub extern "C" fn pumpkin_inject_command(cmd_utf8: *const c_char) {
    if cmd_utf8.is_null() { return; }
    
    if let Some(server) = SERVER_INSTANCE.get() {
        let server_ref: Arc<Server> = server.clone();
        
        let c_str = unsafe { CStr::from_ptr(cmd_utf8) };
        if let Ok(cmd_str) = c_str.to_str() {
            let command = cmd_str.to_string();
            
            if let Some(rt) = RUNTIME.get() {
                rt.spawn(async move {
                    let dispatcher = server_ref.command_dispatcher.read().await;
                    dispatcher.handle_command(
                        &pumpkin::command::CommandSender::Console, 
                        &server_ref, 
                        &command
                    ).await;
                });
            }
        }
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn pumpkin_run_from_config_dir(config_dir_utf8: *const c_char) -> i32 {
    use std::panic;
    let result = panic::catch_unwind(|| unsafe {
        if config_dir_utf8.is_null() { return -1; }

        let c_str = CStr::from_ptr(config_dir_utf8);
        let config_dir_str = match c_str.to_str() {
            Ok(s) => s,
            Err(_) => return -2,
        };

        let config_dir = std::path::PathBuf::from(config_dir_str);

        pumpkin::data::set_data_root(config_dir.clone());

        let rt = match tokio::runtime::Builder::new_multi_thread().enable_all().build() {
            Ok(rt) => rt,
            Err(e) => {
                log::error!("Failed to create runtime: {}", e);
                return -6;
            }
        };
        
        let _ = RUNTIME.set(rt);

        RUNTIME.get().unwrap().block_on(async {
            use pumpkin_config::{BasicConfiguration, AdvancedConfiguration, LoadConfiguration};

            let basic_config = BasicConfiguration::load(&config_dir);
            let advanced_config = AdvancedConfiguration::load(&config_dir);
            let pumpkin_server = PumpkinServer::new(basic_config, advanced_config, &config_dir).await;
            
            let _ = SERVER_INSTANCE.set(pumpkin_server.server.clone());

            pumpkin_server.init_plugins().await;
            pumpkin_server.start().await;
            0
        })
    });

    match result {
        Ok(code) => code,
        Err(_) => -999,
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn pumpkin_request_stop() {
    pumpkin::stop_server();
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn pumpkin_free_string(s: *mut c_char) {
    if !s.is_null() {
        let _ = CString::from_raw(s);
    }
}
