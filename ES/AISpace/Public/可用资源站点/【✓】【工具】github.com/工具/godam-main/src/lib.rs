mod asset_providers;
mod assets;
mod commands;
mod config;
mod console;
mod fs;
mod godot;
mod traits;
mod web_requests;

use clap::Parser;
use commands::*;

#[derive(Parser)]
#[command(version, about, long_about = None, arg_required_else_help = true)]
#[command(propagate_version = true)]
/// godam
///
/// A minimal addon manager for Godot.
pub struct Cli {
    #[command(subcommand)]
    pub command: Command,
}

pub async fn run(command: &Command) -> Result<(), Box<dyn std::error::Error>> {
    match command {
        Command::Init => init::exec()?,
        Command::Search { name } => search::exec(name).await?,
        Command::Install { name } => install::exec(name).await?,
        Command::Uninstall { name } => uninstall::exec(name)?,
        Command::List => list::exec()?,
        Command::Clean => clean::exec()?,
    };

    Ok(())
}
