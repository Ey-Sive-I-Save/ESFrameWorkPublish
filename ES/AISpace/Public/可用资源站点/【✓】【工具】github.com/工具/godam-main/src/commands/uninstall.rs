use indicatif::{MultiProgress, ProgressBar};
use thiserror::Error;

use crate::{
    assets::{self, AssetError},
    config::{Config, ConfigError},
    console::{progress_style, GodamProgressMessage},
    prompt_char,
};

#[derive(Error, Debug)]
pub enum UninstallError {
    #[error(transparent)]
    Config(#[from] ConfigError),

    #[error("IO error: {0}")]
    Io(#[from] std::io::Error),

    #[error(transparent)]
    AssetError(#[from] AssetError),
}

pub fn exec(id: &Option<String>) -> Result<(), UninstallError> {
    let mut config = Config::get()?;

    let progress = MultiProgress::new();

    match id {
        Some(some_id) => uninstall_single(some_id, &mut config, &progress),
        None => {
            let confirm = prompt_char!("Do you want to uninstall all addons? ('y' to confirm)");
            if confirm == 'y' {
                uninstall_all(&mut config, &progress)?;
            }
        }
    }
    Ok(())
}

fn uninstall_single(id: &str, config: &mut Config, progress: &MultiProgress) {
    let pb = progress.add(ProgressBar::new_spinner().with_style(progress_style()));

    let asset = match config.get_asset_info(id) {
        Some(a) => a.clone(),
        None => {
            pb.fail(id, &format!("No addon found with id {id}"));
            return;
        }
    };

    pb.start("Uninstalling", &asset.title);
    match assets::uninstall(id.to_string()) {
        Ok(()) => (),
        Err(e) => {
            pb.fail(id, &e.to_string());
        }
    }
    pb.start("Removing", &asset.title);
    match config.remove_asset(id) {
        Ok(_) => (),
        Err(e) => {
            pb.fail(&asset.title, &e.to_string());
            return;
        }
    }
    pb.complete("Removed", &asset.title);
}

fn uninstall_all(config: &mut Config, progress: &MultiProgress) -> Result<(), UninstallError> {
    for asset in config.asset_infos.clone() {
        uninstall_single(&asset.0, config, progress);
    }
    Ok(())
}
