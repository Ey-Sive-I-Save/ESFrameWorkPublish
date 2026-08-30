FROM barichello/godot-ci:4.4

# setup rust
ENV RUSTUP_HOME=/usr/local/rustup \
    CARGO_HOME=/usr/local/cargo \
    PATH=/usr/local/cargo/bin:$PATH

RUN apt-get update && apt-get install -y \
    curl \
    build-essential \
    libssl-dev \
    pkg-config \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

RUN curl https://sh.rustup.rs -sSf | sh -s -- -y --default-toolchain stable

RUN . $CARGO_HOME/env

# install godam
RUN cargo install godam --version 0.1.2

WORKDIR /workspace

# default cmd
CMD ["godam", "--help"]
