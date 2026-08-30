pub mod asset_archive;
pub mod asset_config;
pub mod asset_source;
pub mod cache;
pub mod consts;

use asset_archive::AssetArchive;
use thiserror::Error;

use crate::{
    config::{Config, ConfigError},
    fs::{
        self,
        path::{get_addons_path, get_install_folder_path, get_out_path_from_archive_path},
        safe_remove_dir,
    },
};

#[derive(Error, Debug)]
pub enum AssetError {
    #[error("Invalid asset structure. No addons folder was identified for asset with id {0}")]
    InvalidAssetStructure(String),
    #[error(transparent)]
    Io(#[from] std::io::Error),
    #[error(transparent)]
    Zip(#[from] zip::result::ZipError),
    #[error("Asset {0} is not installed")]
    NotInstalled(String),
    #[error(transparent)]
    Config(#[from] ConfigError),
}

pub fn install(asset_archive: AssetArchive) -> Result<(), AssetError> {
    let (_, zip_paths_to_extract) = asset_archive.get_plugin_name_and_files_to_extract()?;

    let mut archive = asset_archive.archive;

    for path in zip_paths_to_extract {
        let mut contents = archive.by_name(&path)?;
        let Some(out_path) = get_out_path_from_archive_path(&path) else {
            continue;
        };

        // create parent dir if not exists
        if let Some(parent) = out_path.parent() {
            if !parent.as_os_str().is_empty() && !crate::fs::exists(parent)? {
                crate::fs::safe_create_dir(parent)?;
            }
        }

        // create file
        if !out_path.exists() && !out_path.to_string_lossy().ends_with("/") {
            let mut out_file = crate::fs::create(&out_path)?;
            crate::fs::copy(&mut contents, &mut out_file)?;
        }
    }
    Ok(())
}

pub fn get_install_folders_in_project() -> Result<Vec<String>, AssetError> {
    let addons_path = get_addons_path();

    let folders = fs::get_folders_in_directory(addons_path)?;
    Ok(folders)
}

pub fn uninstall(id: String) -> Result<(), AssetError> {
    let config = Config::get()?;

    match config.get_install_folder(&id) {
        Some(install_folder) => {
            let asset_path = get_install_folder_path(install_folder);
            safe_remove_dir(&asset_path)?;
            Ok(())
        }
        None => Err(AssetError::NotInstalled(id)),
    }
}
