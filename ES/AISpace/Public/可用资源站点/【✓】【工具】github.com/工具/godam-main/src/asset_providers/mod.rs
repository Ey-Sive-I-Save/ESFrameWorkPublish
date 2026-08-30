use semver::Version;
use serde::{Deserialize, Serialize};
use thiserror::Error;

use crate::web_requests::WebRequestError;

pub mod asset_lib;

pub trait AssetProvider {
    /// A lookup by ID to get the metadata of an asset. Metadata is expected to be complete.
    async fn lookup(&self, id: &str) -> Result<Option<AssetMetadata>, AssetProviderError>;
    /// A query by title and optional version to get a list of assets that match the criteria.
    /// Metadata is expected to be partial, containing at least the ID and title.
    async fn query(
        &self, 
        title: &str,
        version: Option<&Version>,
    ) -> Result<Vec<AssetMetadata>, AssetProviderError>;
    /// Downloads the asset by ID and returns the raw bytes wrapped in the AssetBlob newtype.
    async fn download(&self, id: &str) -> Result<AssetBlob, AssetProviderError>;
}

#[derive(Error, Debug)]
pub enum AssetProviderError {
    #[error("Asset was not found.")]
    AssetNotFound,
    #[error(transparent)]
    WebRequest(#[from] WebRequestError),
    #[error(transparent)]
    Parse(#[from] url::ParseError),
}

#[derive(Deserialize, Serialize, Clone, Debug, PartialEq)]
pub struct AssetMetadata {
    pub asset_id: String,
    pub title: String,
    pub download_url: Option<String>,
}

pub struct AssetBlob {
    pub bytes: Vec<u8>,
}
