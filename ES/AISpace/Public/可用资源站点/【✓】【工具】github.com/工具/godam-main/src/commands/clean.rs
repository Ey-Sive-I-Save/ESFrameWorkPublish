use thiserror::Error;

use crate::{assets::cache, config};

#[derive(Error, Debug)]
pub enum CleanError {
    #[error(transparent)]
    Config(#[from] config::ConfigError),
    #[error(transparent)]
    Io(#[from] std::io::Error),
}

pub fn exec() -> Result<(), CleanError> {
    cache::clear()?;
    Ok(())
}
