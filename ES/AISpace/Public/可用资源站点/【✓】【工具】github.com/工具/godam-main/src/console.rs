use console::style;
use indicatif::{ProgressBar, ProgressStyle};

pub static ORANGE: u8 = 214;
pub static BLUE: u8 = 39;

pub fn progress_style() -> ProgressStyle {
    ProgressStyle::with_template("{spinner:.cyan:>2} {msg}").unwrap()
}

#[macro_export]
macro_rules! info {
    ($($arg:tt)*) => {{
        println!("  {}", console::style(format_args!($($arg)*)).color256($crate::console::BLUE))
    }};
}

#[macro_export]
macro_rules! warn {
    ($($arg:tt)*) => {{
        println!("  {}", console::style(format_args!($($arg)*)).color256($crate::console::ORANGE))
    }};
}

#[macro_export]
macro_rules! prompt_char {
    ($($arg:tt)*) => {{
        println!("  {}", console::style(format_args!($($arg)*)).bold().color256($crate::console::ORANGE));
        console::Term::stdout().read_char()?
    }};
}

pub trait GodamProgressMessage {
    fn start(&self, action: &str, msg: &str);
    fn complete(&self, action: &str, msg: &str);
    fn fail(&self, msg: &str, reason: &str);
}

impl GodamProgressMessage for ProgressBar {
    fn start(&self, action: &str, msg: &str) {
        self.set_message(format!(
            "{} {}",
            style(action).color256(BLUE).dim(),
            style(msg).white().dim()
        ));
    }

    fn complete(&self, action: &str, msg: &str) {
        self.finish_with_message(format!(
            "{} {}",
            style(action).color256(BLUE),
            style(msg).white()
        ));
    }

    fn fail(&self, msg: &str, reason: &str) {
        self.abandon_with_message(format!(
            "{}: {} ({reason})",
            style("Failed").color256(ORANGE),
            style(msg).white()
        ));
    }
}
