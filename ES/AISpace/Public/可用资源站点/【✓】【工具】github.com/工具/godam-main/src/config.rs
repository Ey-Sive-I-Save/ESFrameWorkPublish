use std::collections::BTreeMap;

use semver::Version;
use serde::{Deserialize, Serialize};
use thiserror::Error;

use crate::{
    asset_providers::AssetMetadata,
    fs::{
        path::{get_addons_path, get_config_path, get_gitignore_path},
        ADDONS_GITIGNORE_CONTENT,
    },
    godot::{self, project::GodotProjectError},
};

#[derive(Error, Debug)]
pub enum ConfigError {
    #[error("IO error: {0}")]
    Io(#[from] std::io::Error),

    #[error("Parse error: {0}")]
    Parse(#[from] toml::de::Error),

    #[error("Parse error: {0}")]
    Serialize(#[from] toml::ser::Error),

    #[error("Godot error: {0}")]
    GodotError(#[from] GodotProjectError),
    #[error("Project is not initialized, try 'godam init'.")]
    Uninitialized,
}

#[derive(Serialize, Deserialize)]
pub struct Config {
    pub godot_version: Version,
    pub asset_infos: BTreeMap<String, AssetMetadata>,
    pub install_folders: BTreeMap<String, String>,
}

impl Config {
    pub fn get() -> Result<Self, ConfigError> {
        let config_path = get_config_path();
        let string = crate::fs::read_string(config_path).map_err(|_| ConfigError::Uninitialized)?;
        let config = toml::from_str(&string)?;

        Ok(config)
    }

    pub fn get_asset_info(&self, id: &str) -> Option<&AssetMetadata> {
        self.asset_infos.get(id)
    }

    pub fn get_install_folder(&self, asset_id: &str) -> Option<&String> {
        self.install_folders.get(asset_id)
    }

    pub fn set_install_folder(
        &mut self,
        id: &str,
        install_folder: String,
    ) -> Result<(), ConfigError> {
        self.install_folders.insert(id.to_string(), install_folder);
        self.save()
    }

    pub fn init() -> Result<(), ConfigError> {
        let version = godot::project::get_version()?;

        let config = Config {
            asset_infos: BTreeMap::new(),
            godot_version: version,
            install_folders: BTreeMap::new(),
        };

        let contents = toml::to_string(&config)?;

        let addons_path = get_addons_path();
        if !crate::fs::exists(addons_path)? {
            crate::fs::safe_create_dir(addons_path)?;
        }
        crate::fs::safe_write(get_config_path(), contents)?;
        crate::fs::safe_write(get_gitignore_path(), ADDONS_GITIGNORE_CONTENT)?;

        Ok(())
    }

    pub fn add_asset(&mut self, id: String, asset: AssetMetadata) -> Result<(), ConfigError> {
        self.asset_infos.insert(id, asset);
        self.save()
    }

    pub fn remove_asset(
        &mut self,
        id: &str,
    ) -> Result<(Option<AssetMetadata>, Option<String>), ConfigError> {
        let removed_info = self.asset_infos.remove(id);
        let removed_folder = self.install_folders.remove(id);
        self.save()?;

        Ok((removed_info, removed_folder))
    }

    pub fn save(&self) -> Result<(), ConfigError> {
        let config_path = get_config_path();
        let str = toml::to_string(self)?;
        Ok(crate::fs::safe_write(config_path, str)?)
    }
}
