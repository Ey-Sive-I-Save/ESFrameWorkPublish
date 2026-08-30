use crate::{
    config::{self, Config},
    info, warn,
};

pub fn exec() -> Result<(), config::ConfigError> {
    if Config::get().is_err() {
        Config::init()?;

        info!(
            "godam: Project is now using godam. Search for assets using 'godam search <name>' and install them using 'godam install <ID>'"
        );
    } else {
        warn!("godam: Project is already set up to use godam. Search for assets using 'godam search <name>' and install them using 'godam install <ID>'");
    }
    Ok(())
}
