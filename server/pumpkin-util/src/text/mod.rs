use crate::text::color::ARGBColor;
use crate::translation::{Locale, get_translation, get_translation_text, reorder_substitutions};
use click::ClickEvent;
use color::Color;
use colored::Colorize;
use core::str;
use hover::HoverEvent;
use serde::de::{Error, MapAccess, SeqAccess, Visitor};
use serde::{Deserialize, Deserializer, Serialize};
use std::borrow::Cow;
use std::fmt::Formatter;
use style::Style;

pub mod click;
pub mod color;
pub mod hover;
pub mod style;

/// Represents a text component
#[derive(Clone, Debug, PartialEq, Eq, Hash)]
pub struct TextComponent(pub TextComponentBase);

impl<'de> Deserialize<'de> for TextComponent {
    fn deserialize<D: Deserializer<'de>>(deserializer: D) -> Result<Self, D::Error> {
        struct TextComponentVisitor;

        impl<'de> Visitor<'de> for TextComponentVisitor {
            type Value = TextComponentBase;

            fn expecting(&self, formatter: &mut Formatter) -> std::fmt::Result {
                formatter.write_str("a TextComponentBase or a sequence of TextComponentBase")
            }

            fn visit_str<E: Error>(self, v: &str) -> Result<Self::Value, E> {
                Ok(TextComponentBase {
                    content: TextContent::Text {
                        text: Cow::from(v.to_string()),
                    },
                    style: Default::default(),
                    extra: vec![],
                })
            }

            fn visit_seq<A: SeqAccess<'de>>(self, mut seq: A) -> Result<Self::Value, A::Error> {
                let mut bases = Vec::new();
                while let Some(element) = seq.next_element::<TextComponent>()? {
                    bases.push(element.0);
                }

                Ok(TextComponentBase {
                    content: TextContent::Text { text: "".into() },
                    style: Default::default(),
                    extra: bases,
                })
            }

            fn visit_map<A: MapAccess<'de>>(self, map: A) -> Result<Self::Value, A::Error> {
                TextComponentBase::deserialize(serde::de::value::MapAccessDeserializer::new(map))
            }
        }

        deserializer
            .deserialize_any(TextComponentVisitor)
            .map(TextComponent)
    }
}

impl Serialize for TextComponent {
    fn serialize<S: serde::Serializer>(&self, serializer: S) -> Result<S::Ok, S::Error> {
        serializer.serialize_newtype_struct("TextComponent", &self.0.to_translated())
    }
}

#[derive(Clone, Debug, Serialize, Deserialize, PartialEq, Eq, Hash)]
#[serde(rename_all = "camelCase")]
pub struct TextComponentBase {
    /// The actual text
    #[serde(flatten)]
    pub content: TextContent,
    /// Style of the text. Bold, Italic, underline, Color...
    /// Also has `ClickEvent`
    #[serde(flatten)]
    pub style: Box<Style>,
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    /// Extra text components
    pub extra: Vec<TextComponentBase>,
}

impl TextComponentBase {
    /// Convert a full TextComponent tree into ANSI + OSC‑8 console output
    pub fn to_pretty_console(&self) -> String {
        let mut out = String::new();
        Self::render_component(self, &mut out, &Style::default());
        out
    }

    fn render_component(component: &TextComponentBase, out: &mut String, parent_style: &Style) {
        let merged = parent_style.merge(&component.style);

        let mut rendered = match &component.content {
            TextContent::Text { text } => text.to_string(),

            TextContent::Translate { translate, with } => get_translation_text(
                format!("minecraft:{translate}"),
                Locale::EnUs,
                with.to_vec(),
            ),

            TextContent::EntityNames { selector, .. } => selector.to_string(),

            TextContent::Keybind { keybind } => keybind.to_string(),

            TextContent::Custom { key, with, .. } => {
                get_translation_text(key.to_string(), Locale::EnUs, with.to_vec())
            }
        };

        rendered = merged.apply_ansi(&rendered);

        if let Some(click) = &merged.click_event {
            rendered = wrap_click_event_osc8(click, &rendered);
        }

        if let Some(hover) = &merged.hover_event {
            rendered = wrap_hover_event_osc8(hover, &rendered);
        }

        out.push_str(&rendered);

        for (i, child) in component.extra.iter().enumerate() {
            if i > 0 {
                out.push('\n');
            }

            Self::render_component(child, out, &merged);
        }
    }

    pub fn get_text(&self, locale: Locale) -> String {
        match &self.content {
            TextContent::Text { text } => text.to_string(),

            TextContent::Translate { translate, with } => {
                get_translation_text(format!("minecraft:{translate}"), locale, with.clone())
            }

            TextContent::EntityNames { selector, .. } => selector.to_string(),

            TextContent::Keybind { keybind } => keybind.to_string(),

            TextContent::Custom { key, with, .. } => {
                get_translation_text(key.clone(), locale, with.clone())
            }
        }
    }

    pub fn to_translated(&self) -> Self {
        // Divide the translation into slices and inserts the substitutions
        let component = match &self.content {
            TextContent::Custom { key, with, locale } => {
                let translation = get_translation(key, *locale);
                let mut translation_parent = translation.clone();
                let mut translation_slices = vec![];

                if translation.contains('%') {
                    let (substitutions, ranges) = reorder_substitutions(&translation, with.clone());

                    for (idx, &range) in ranges.iter().enumerate() {
                        if idx == 0 {
                            translation_parent = translation[..range.start].to_string();
                        }

                        translation_slices.push(substitutions[idx].clone());

                        if range.end < translation.len() - 1 {
                            let next = if idx == ranges.len() - 1 {
                                &translation[range.end + 1..]
                            } else {
                                &translation[range.end + 1..ranges[idx + 1].start]
                            };

                            translation_slices.push(TextComponentBase {
                                content: TextContent::Text {
                                    text: Cow::Owned(next.to_string()),
                                },
                                style: Box::new(Style::default()),
                                extra: vec![],
                            });
                        }
                    }
                }

                for extra in &self.extra {
                    translation_slices.push(extra.clone());
                }

                TextComponentBase {
                    content: TextContent::Text {
                        text: translation_parent.into(),
                    },
                    style: self.style.clone(),
                    extra: translation_slices,
                }
            }

            _ => self.clone(),
        };

        let extra = component
            .extra
            .iter()
            .map(|c| c.to_translated())
            .collect::<Vec<_>>();

        let style = match &component.style.hover_event {
            None => component.style.clone(),

            Some(hover) => {
                let mut style = component.style.clone();

                style.hover_event = match hover {
                    HoverEvent::ShowText { value } => Some(HoverEvent::ShowText {
                        value: value.iter().map(|v| v.to_translated()).collect(),
                    }),

                    HoverEvent::ShowEntity { name, id, uuid } => Some(HoverEvent::ShowEntity {
                        name: name
                            .as_ref()
                            .map(|n| n.iter().map(|v| v.to_translated()).collect()),
                        id: id.clone(),
                        uuid: uuid.clone(),
                    }),

                    HoverEvent::ShowItem { id, count } => Some(HoverEvent::ShowItem {
                        id: id.clone(),
                        count: *count,
                    }),
                };

                style
            }
        };

        TextComponentBase {
            content: component.content.clone(),
            style,
            extra,
        }
    }
}

impl Style {
    pub fn merge(&self, child: &Style) -> Style {
        Style {
            color: child.color.or(self.color),
            bold: child.bold.or(self.bold),
            italic: child.italic.or(self.italic),
            underlined: child.underlined.or(self.underlined),
            strikethrough: child.strikethrough.or(self.strikethrough),
            obfuscated: child.obfuscated.or(self.obfuscated),
            insertion: child.insertion.clone().or(self.insertion.clone()),
            click_event: child.click_event.clone().or(self.click_event.clone()),
            hover_event: child.hover_event.clone().or(self.hover_event.clone()),
            font: child.font.clone().or(self.font.clone()),
            shadow_color: child.shadow_color.or(self.shadow_color),
        }
    }

    pub fn apply_ansi(&self, text: &str) -> String {
        let mut out = text.to_string();

        if let Some(color) = self.color {
            out = color.console_color(&out).to_string();
        }
        if self.bold == Some(true) {
            out = out.bold().to_string();
        }
        if self.italic == Some(true) {
            out = out.italic().to_string();
        }
        if self.underlined == Some(true) {
            out = out.underline().to_string();
        }
        if self.strikethrough == Some(true) {
            out = out.strikethrough().to_string();
        }
        if self.obfuscated == Some(true) {
            out = obfuscate_text(&out);
        }
        if self.shadow_color.is_some() {
            out = format!("\x1B[2m{out}\x1B[22m"); // dim
        }

        out
    }
}

fn wrap_click_event_osc8(click: &ClickEvent, visible_text: &str) -> String {
    let target = match click {
        ClickEvent::OpenUrl { url } => url.to_string(),
        ClickEvent::OpenFile { path } => format!("file://{}", path),
        ClickEvent::RunCommand { command } => format!("mc:run_command:{}", command),
        ClickEvent::SuggestCommand { command } => format!("mc:suggest_command:{}", command),
        ClickEvent::ChangePage { page } => format!("mc:change_page:{}", page),
        ClickEvent::CopyToClipboard { value } => format!("mc:copy_to_clipboard:{}", value),
    };

    format!("\x1B]8;;{target}\x1B\\{visible_text}\x1B]8;;\x1B\\")
}

fn wrap_hover_event_osc8(hover: &HoverEvent, visible_text: &str) -> String {
    let tooltip = match hover {
        HoverEvent::ShowText { value } => value
            .iter()
            .map(|v| v.to_pretty_console())
            .collect::<String>(),

        HoverEvent::ShowItem { id, count } => {
            let count_str = count
                .map(|c| c.to_string())
                .unwrap_or_else(|| "?".to_string());
            format!("Item: {id} x{count_str}")
        }

        HoverEvent::ShowEntity { id, name, .. } => {
            let name_str = name
                .as_ref()
                .map(|n| n.iter().map(|x| x.to_pretty_console()).collect::<String>())
                .unwrap_or_default();
            format!("Entity: {id} {name_str}")
        }
    };

    format!("\x1B]8;;tooltip:{tooltip}\x1B\\{visible_text}\x1B]8;;\x1B\\")
}

fn obfuscate_text(text: &str) -> String {
    use rand::{Rng, rng};
    let mut rng = rng();
    text.chars()
        .map(|c| {
            if c.is_whitespace() {
                c
            } else {
                rng.random_range('a'..'z')
            }
        })
        .collect()
}

impl TextComponent {
    pub fn text<P: Into<Cow<'static, str>>>(plain: P) -> Self {
        Self(TextComponentBase {
            content: TextContent::Text { text: plain.into() },
            style: Box::new(Style::default()),
            extra: vec![],
        })
    }

    pub fn translate<K: Into<Cow<'static, str>>, W: Into<Vec<TextComponent>>>(
        key: K,
        with: W,
    ) -> Self {
        Self(TextComponentBase {
            content: TextContent::Translate {
                translate: key.into(),
                with: with.into().into_iter().map(|x| x.0).collect(),
            },
            style: Box::new(Style::default()),
            extra: vec![],
        })
    }

    pub fn custom<K: Into<Cow<'static, str>>, W: Into<Vec<TextComponent>>>(
        namespace: K,
        key: K,
        locale: Locale,
        with: W,
    ) -> Self {
        Self(TextComponentBase {
            content: TextContent::Custom {
                key: format!("{}:{}", namespace.into(), key.into())
                    .to_lowercase()
                    .into(),
                locale,
                with: with.into().into_iter().map(|x| x.0).collect(),
            },
            style: Box::new(Style::default()),
            extra: vec![],
        })
    }

    pub fn add_child(mut self, child: TextComponent) -> Self {
        self.0.extra.push(child.0);
        self
    }

    pub fn from_content(content: TextContent) -> Self {
        Self(TextComponentBase {
            content,
            style: Box::new(Style::default()),
            extra: vec![],
        })
    }

    pub fn add_text<P: Into<Cow<'static, str>>>(mut self, text: P) -> Self {
        self.0.extra.push(TextComponentBase {
            content: TextContent::Text { text: text.into() },
            style: Box::new(Style::default()),
            extra: vec![],
        });
        self
    }

    pub fn get_text(self) -> String {
        self.0.get_text(Locale::EnUs)
    }

    pub fn chat_decorated(format: String, player_name: String, content: String) -> Self {
        // Todo: maybe allow players to use & in chat contingent on permissions
        let with_resolved_fields = format
            .replace('&', "§")
            .replace("{DISPLAYNAME}", player_name.as_str())
            .replace("{MESSAGE}", content.as_str());

        Self(TextComponentBase {
            content: TextContent::Text {
                text: Cow::Owned(with_resolved_fields),
            },
            style: Box::new(Style::default()),
            extra: vec![],
        })
    }

    pub fn to_pretty_console(self) -> String {
        self.0.to_pretty_console()
    }
}

impl TextComponent {
    pub fn encode(&self) -> Box<[u8]> {
        let mut buf = Vec::new();
        // TODO: Properly handle errors
        pumpkin_nbt::serializer::to_bytes_unnamed(&self, &mut buf)
            .expect("Failed to serialize text component NBT for encode");

        buf.into_boxed_slice()
    }

    pub fn color(mut self, color: Color) -> Self {
        self.0.style.color = Some(color);
        self
    }

    pub fn color_named(mut self, color: color::NamedColor) -> Self {
        self.0.style.color = Some(Color::Named(color));
        self
    }

    pub fn color_rgb(mut self, color: color::RGBColor) -> Self {
        self.0.style.color = Some(Color::Rgb(color));
        self
    }

    /// Makes the text bold
    pub fn bold(mut self) -> Self {
        self.0.style.bold = Some(true);
        self
    }

    /// Makes the text italic
    pub fn italic(mut self) -> Self {
        self.0.style.italic = Some(true);
        self
    }

    /// Makes the text underlined
    pub fn underlined(mut self) -> Self {
        self.0.style.underlined = Some(true);
        self
    }

    /// Makes the text strikethrough
    pub fn strikethrough(mut self) -> Self {
        self.0.style.strikethrough = Some(true);
        self
    }

    /// Makes the text obfuscated
    pub fn obfuscated(mut self) -> Self {
        self.0.style.obfuscated = Some(true);
        self
    }

    /// When the text is shift-clicked by a player, this string is inserted in their chat input. It does not overwrite any existing text the player was writing. This only works in chat messages.
    pub fn insertion(mut self, text: String) -> Self {
        self.0.style.insertion = Some(text);
        self
    }

    /// Allows for events to occur when the player clicks on text. Only works in chat.
    pub fn click_event(mut self, event: ClickEvent) -> Self {
        self.0.style.click_event = Some(event);
        self
    }

    /// Allows for a tooltip to be displayed when the player hovers their mouse over text.
    pub fn hover_event(mut self, event: HoverEvent) -> Self {
        self.0.style.hover_event = Some(event);
        self
    }

    /// Allows you to change the font of the text.
    /// Default fonts: `minecraft:default`, `minecraft:uniform`, `minecraft:alt`, `minecraft:illageralt`
    pub fn font(mut self, resource_location: String) -> Self {
        self.0.style.font = Some(resource_location);
        self
    }

    /// Overrides the shadow properties of text.
    pub fn shadow_color(mut self, color: ARGBColor) -> Self {
        self.0.style.shadow_color = Some(color);
        self
    }
}

#[derive(Clone, Debug, Serialize, Deserialize, PartialEq, Eq, Hash)]
#[serde(untagged)]
pub enum TextContent {
    /// Raw text
    Text { text: Cow<'static, str> },
    /// Translated text
    Translate {
        translate: Cow<'static, str>,
        #[serde(default, skip_serializing_if = "Vec::is_empty")]
        with: Vec<TextComponentBase>,
    },
    /// Displays the name of one or more entities found by a selector.
    EntityNames {
        selector: Cow<'static, str>,
        #[serde(default, skip_serializing_if = "Option::is_none")]
        separator: Option<Cow<'static, str>>,
    },
    /// A keybind identifier
    /// https://minecraft.wiki/w/Controls#Configurable_controls
    Keybind { keybind: Cow<'static, str> },
    /// A custom translation key
    #[serde(skip)]
    Custom {
        key: Cow<'static, str>,
        locale: Locale,
        with: Vec<TextComponentBase>,
    },
}

#[cfg(test)]
mod test {
    use pumpkin_nbt::serializer::to_bytes_unnamed;

    use crate::text::{TextComponent, color::NamedColor};

    #[test]
    fn test_serialize_text_component() {
        let msg_comp = TextComponent::translate(
            "multiplayer.player.joined",
            [TextComponent::text("NAME".to_string())],
        )
        .color_named(NamedColor::Yellow);

        let mut bytes = Vec::new();
        to_bytes_unnamed(&msg_comp.0, &mut bytes).unwrap();

        let expected_bytes = [
            0x0A, 0x08, 0x00, 0x09, 0x74, 0x72, 0x61, 0x6E, 0x73, 0x6C, 0x61, 0x74, 0x65, 0x00,
            0x19, 0x6D, 0x75, 0x6C, 0x74, 0x69, 0x70, 0x6C, 0x61, 0x79, 0x65, 0x72, 0x2E, 0x70,
            0x6C, 0x61, 0x79, 0x65, 0x72, 0x2E, 0x6A, 0x6F, 0x69, 0x6E, 0x65, 0x64, 0x09, 0x00,
            0x04, 0x77, 0x69, 0x74, 0x68, 0x0A, 0x00, 0x00, 0x00, 0x01, 0x08, 0x00, 0x04, 0x74,
            0x65, 0x78, 0x74, 0x00, 0x04, 0x4E, 0x41, 0x4D, 0x45, 0x00, 0x08, 0x00, 0x05, 0x63,
            0x6F, 0x6C, 0x6F, 0x72, 0x00, 0x06, 0x79, 0x65, 0x6C, 0x6C, 0x6F, 0x77, 0x00,
        ];

        assert_eq!(bytes, expected_bytes);
    }
}
