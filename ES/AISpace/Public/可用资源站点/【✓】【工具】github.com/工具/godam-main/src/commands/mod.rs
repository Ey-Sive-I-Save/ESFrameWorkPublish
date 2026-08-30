pub mod clean;
pub mod init;
pub mod install;
pub mod list;
pub mod search;
pub mod uninstall;

use clap::Subcommand;

#[derive(Subcommand)]
pub enum Command {
    #[command()]
    /// Initializes your Godot project to use godam as your addon manager
    Init,
    /// Searches the Godot Asset Library API for assets by name.
    #[command(alias = "s")]
    Search {
        #[arg(index = 1)]
        name: String,
    },
    /// Installs the specified addon to your Godot project, adding it to the godam configuration.

    #[command(alias = "i")]
    Install {
        /// The name of the asset you want to install
        #[arg(index = 1)]
        name: Option<Vec<String>>,
    },
    /// Uninstalls the specified addon from your Godot project, removing it from the godam configuration.
    #[command(alias = "u")]
    Uninstall {
        /// The name of the asset you want to uninstall
        #[arg(index = 1)]
        name: Option<String>,
    },
    /// Lists all assets being managed by Godam
    #[command(alias = "ls", alias = "l")]
    ///
    List,
    /// Cleans the godam cache folder
    #[command(alias = "c")]
    Clean,
}
