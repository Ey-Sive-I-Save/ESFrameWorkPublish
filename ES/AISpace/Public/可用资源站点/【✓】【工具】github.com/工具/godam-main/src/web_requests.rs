use reqwest::Url;
use serde::Deserialize;
use thiserror::Error;

#[derive(Error, Debug)]
pub enum WebRequestError {
    #[error(transparent)]
    ReqwestError(#[from] reqwest::Error),
}

pub async fn get_json<T: for<'de> Deserialize<'de>>(url: Url) -> Result<T, WebRequestError> {
    let res = reqwest::get(url.clone()).await?;

    let json = res.json::<T>().await?;

    Ok(json)
}

pub async fn get_blob(url: Url) -> Result<Vec<u8>, WebRequestError> {
    let res = reqwest::get(url.clone()).await?;

    let bytes = res.bytes().await?;

    Ok(bytes.to_vec())
}
