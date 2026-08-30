use crate::{
    asset_providers::AssetMetadata,
    config::{Config, ConfigError},
    info,
};

pub fn exec() -> Result<(), ConfigError> {
    let config = Config::get()?;

    if config.asset_infos.is_empty() {
        info!("No assets found.");
        return Ok(());
    }

    let longest_id_length = config
        .asset_infos
        .iter()
        .max_by(|a, b| a.0.len().cmp(&b.0.len()))
        .expect("one is longest")
        .0
        .len();

    for (id, AssetMetadata { title, .. }) in config.asset_infos {
        info!("{id:>width$}: {title}", width = longest_id_length,)
    }

    Ok(())
}
