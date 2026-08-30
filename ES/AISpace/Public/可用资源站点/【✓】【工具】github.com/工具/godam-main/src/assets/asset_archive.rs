use zip::ZipArchive;

use crate::traits::ReadSeek;

use super::{consts, AssetError};

pub struct AssetArchive {
    pub id: String,
    pub archive: ZipArchive<Box<dyn ReadSeek>>,
}

impl AssetArchive {
    pub fn get_plugin_name_and_files_to_extract(
        &self,
    ) -> Result<(String, Vec<String>), AssetError> {
        let Some((plugin_name, plugin_path)) = self.get_plugin_info() else {
            return Err(AssetError::InvalidAssetStructure(self.id.to_string()));
        };

        let file_paths = self
            .archive
            .file_names()
            .filter(|file_name| file_name.starts_with(&plugin_path))
            .map(String::from)
            .collect();

        Ok((plugin_name, file_paths))
    }

    pub fn get_plugin_info(&self) -> Option<(String, String)> {
        self.archive.file_names().find_map(|file_name| {
            let mut parts = file_name.split('/');
            let mut full_path = Vec::new();

            // Check if "addons" is the first part or the second part
            if let Some(first_part) = parts.next() {
                full_path.push(first_part);

                if first_part == consts::ADDONS_PART_PATTERN {
                    if let Some(plugin_folder) = parts.next() {
                        if !plugin_folder.is_empty() {
                            full_path.push(plugin_folder);
                            return Some((plugin_folder.to_string(), full_path.join("/")));
                        }
                    }
                }
            }

            // If not found in the first part, check for "addons" in the next layer
            if let Some(second_part) = parts.next() {
                full_path.push(second_part);

                if second_part == consts::ADDONS_PART_PATTERN {
                    if let Some(plugin_folder) = parts.next() {
                        if !plugin_folder.is_empty() {
                            full_path.push(plugin_folder);
                            return Some((plugin_folder.to_string(), full_path.join("/")));
                        }
                    }
                }
            }

            None
        })
    }
}
