use thiserror::Error;

use crate::{
    asset_providers::{asset_lib::AssetLib, AssetMetadata, AssetProvider, AssetProviderError},
    godot::project::{get_version, GodotProjectError},
    info,
};
#[derive(Error, Debug)]
pub enum SearchError {
    #[error(transparent)]
    Godot(#[from] GodotProjectError),
    #[error(transparent)]
    AssetProvider(#[from] AssetProviderError),
}

pub async fn exec(asset_name: &str) -> Result<(), SearchError> {
    let version = get_version()?;
    let assets = AssetLib.query(asset_name, Some(&version)).await?;

    for AssetMetadata {
        title, asset_id, ..
    } in &assets
    {
        info!("{asset_id}: {title}");
    }

    Ok(())
}
