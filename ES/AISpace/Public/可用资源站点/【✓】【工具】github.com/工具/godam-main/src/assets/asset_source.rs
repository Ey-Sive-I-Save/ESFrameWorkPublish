use serde::{Deserialize, Serialize};

#[derive(Serialize, Deserialize, Clone, Debug, PartialEq)]
pub enum AssetSource {
    AssetLib,
    Local,
    Git,
}
