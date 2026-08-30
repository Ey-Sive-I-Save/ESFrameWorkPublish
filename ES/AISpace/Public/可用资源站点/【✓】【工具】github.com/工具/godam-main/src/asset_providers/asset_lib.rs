//! Handles all calls to the web

use reqwest::Url;
use semver::Version;
use serde::{Deserialize, Serialize};

use crate::web_requests::{self};

use super::{AssetBlob, AssetMetadata, AssetProvider, AssetProviderError};

#[derive(Deserialize, Serialize, Clone)]
pub struct AssetResponse {
    result: Vec<AssetMetadata>,
}

#[derive(Deserialize, Serialize, Clone)]
pub struct AssetSearchResponse {
    result: Vec<AssetMetadata>,
}

#[derive(Deserialize, Serialize, Clone, Debug, PartialEq)]
pub struct AssetSearchResult {
    pub asset_id: String,
    pub title: String,
}

const ASSET_LIBRARY_ASSET_URL: &str = "https://godotengine.org/asset-library/api/asset";

pub struct AssetLib;

impl AssetProvider for AssetLib {
    async fn query(
        &self,
        name: &str,
        version: Option<&Version>,
    ) -> Result<Vec<AssetMetadata>, AssetProviderError> {
        let url = match version {
            Some(version) => Url::parse_with_params(
                ASSET_LIBRARY_ASSET_URL,
                &[("filter", name), ("godot_version", &version.to_string())],
            )?,
            None => Url::parse_with_params(ASSET_LIBRARY_ASSET_URL, &[("filter", name)])?,
        };

        let asset_search_response = web_requests::get_json::<AssetSearchResponse>(url).await?;

        Ok(asset_search_response.result)
    }

    async fn lookup(&self, id: &str) -> Result<Option<AssetMetadata>, AssetProviderError> {
        let request_url = Url::parse(&format!("{ASSET_LIBRARY_ASSET_URL}/{id}"))?;

        let asset = web_requests::get_json::<AssetMetadata>(request_url).await?;

        Ok(Some(asset))
    }

    async fn download(&self, id: &str) -> Result<AssetBlob, AssetProviderError> {
        let metadata = self.lookup(id).await?;

        let Some(metadata) = metadata else {
            return Err(AssetProviderError::AssetNotFound);
        };

        let Some(download_url_string) = &metadata.download_url else {
            return Err(AssetProviderError::AssetNotFound);
        };

        let download_url = Url::parse(download_url_string)?;
        let bytes = web_requests::get_blob(download_url).await?;

        Ok(AssetBlob { bytes })
    }
}
