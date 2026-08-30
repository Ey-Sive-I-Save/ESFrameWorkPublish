use clap::Parser;
use godam::Cli;

#[tokio::main]
async fn main() {
    let cli = Cli::parse();

    if let Err(e) = godam::run(&cli.command).await {
        eprintln!("godam: {e}");
    }
}
