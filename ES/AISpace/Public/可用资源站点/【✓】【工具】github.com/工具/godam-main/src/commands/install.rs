use std::{
    io::Cursor,
    sync::{Arc, Mutex},
};

use indicatif::{MultiProgress, ProgressBar};
use thiserror::Error;
use tokio::task::JoinSet;
use zip::ZipArchive;

use crate::{
    asset_providers::{asset_lib::AssetLib, AssetMetadata, AssetProvider, AssetProviderError},
    assets::{self, asset_archive::AssetArchive, cache, get_install_folders_in_project},
    config::{self, Config},
    console::{progress_style, GodamProgressMessage},
    traits::ReadSeek,
    warn,
};

#[derive(Error, Debug)]
pub enum InstallError {
    #[error(transparent)]
    Config(#[from] config::ConfigError),

    #[error(transparent)]
    AssetProvider(#[from] AssetProviderError),

    #[error("Cache error: {0}")]
    Cache(#[from] std::io::Error),

    #[error(transparent)]
    Zip(#[from] zip::result::ZipError),

    #[error(transparent)]
    Asset(#[from] assets::AssetError),

    #[error("An error occured when locking resources for a thread.")]
    Mutex,
}

pub async fn exec(ids: &Option<Vec<String>>) -> Result<(), InstallError> {
    let mut config = Config::get()?;

    if let Some(ids) = ids {
        for id in ids {
            if config.get_asset_info(id).is_none() {
                match AssetLib.lookup(id).await {
                    Ok(Some(asset)) => config.add_asset(id.to_string(), asset)?,
                    Ok(None) => {
                        warn!("No asset found with id {id}");
                        continue;
                    }
                    Err(e) => warn!("{e}"),
                }
            }
        }
    }

    let progress = MultiProgress::new();

    let assets = Config::get()?.asset_infos;
    let install_folders = get_install_folders_in_project()?;

    let not_installed_assets: Vec<(String, AssetMetadata)> = assets
        .into_iter()
        .filter_map(|entry| {
            let Some(folder) = config.get_install_folder(&entry.0) else {
                return Some(entry);
            };

            if install_folders.contains(folder) {
                None
            } else {
                Some(entry)
            }
        })
        .collect();

    let config = Arc::new(Mutex::new(config));

    let mut tasks = JoinSet::new();

    for (id, asset) in not_installed_assets {
        let config = config.clone();
        let pb = progress.add(ProgressBar::new_spinner().with_style(progress_style()));
        tasks.spawn(async move {
            pb.enable_steady_tick(std::time::Duration::from_millis(100));
            match install_asset(&id, &asset, &pb, config).await {
                Ok(()) => pb.complete("Installed", &asset.title),
                Err(e) => pb.fail(&asset.title, &e.to_string()),
            };
        });
    }

    tasks.join_all().await;

    Ok(())
}

async fn install_asset(
    id: &str,
    asset: &AssetMetadata,
    progress: &ProgressBar,
    config: Arc<Mutex<Config>>,
) -> Result<(), InstallError> {
    progress.start("Fetching", &asset.title);
    let archive: AssetArchive = match cache::get(id) {
        Ok(hit) => hit,

        Err(_) => {
            let blob = AssetLib.download(&asset.asset_id).await?;
            cache::write_to_cache(id, &blob)?;
            let cursor: Box<dyn ReadSeek> = Box::new(Cursor::new(blob.bytes));
            AssetArchive {
                id: id.to_string(),
                archive: ZipArchive::new(cursor)?,
            }
        }
    };

    // register install folder before installing
    match config.lock() {
        Ok(mut config) => {
            let install_folder_name = match archive.get_plugin_info() {
                Some((name, _)) => name,
                None => {
                    return Err(InstallError::Asset(
                        assets::AssetError::InvalidAssetStructure(
                            "Could not find plugin name".to_string(),
                        ),
                    ))
                }
            };
            if let Err(e) = config.set_install_folder(id, install_folder_name) {
                progress.fail(&asset.title, &e.to_string());
            }
        }
        Err(e) => {
            progress.fail(&asset.title, &e.to_string());
            return Err(InstallError::Mutex);
        }
    }

    progress.start("Unpacking", &asset.title);
    match assets::install(archive).map_err(InstallError::from) {
        Ok(folder) => folder,
        Err(e) => {
            progress.fail(&asset.title, &e.to_string());
            return Err(e);
        }
    };

    progress.complete("Installed", &asset.title);

    Ok(())
}
