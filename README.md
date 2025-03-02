# Duckit

## Overview

![duckit](https://github.com/user-attachments/assets/27f0b632-27ef-457f-bf58-bad6596de3c3)

Duckit is a simple search tool that queries DuckDuckGo Lite for results based on user-defined search terms. It allows users to specify valid websites to filter results and the maximum number of pages to search. The configuration can be loaded from a file, and if no configuration is found, default values are used.

## Features

* Load configuration from a TOML file.
* Specify valid websites to filter search results.
* Set a maximum number of results to display.
* Support for subtopics to refine search results.
* Interactive mode for repeatedly prompting the user for queries.

## Configuration

The configuration can be specified in a TOML file with the following options:

* `sites`: A list of websites to filter results (e.g., `["reddit.com", "stackoverflow.com"]`).
* `repl`: A boolean indicating whether to enable interactive mode.
* `search_engine`: A string specifying the search engine to use (currently only supports DuckDuckGo).
* `subtopic`: A list of subtopics to refine search results.

### Example `config.toml`

```toml
[browser]
sites = ["reddit.com", "stackoverflow.com"]
repl = true
search_engine = "duckduckgo"
subtopic = ["quicksort", "jvm"]
```

### Default Configuration

If no configuration file is found, the following default values are used:

* **Valid Websites**: None
* **Max Results**: 10
* **Search Engine**: DuckDuckGo
* **Subtopics**: None

## Usage

You can run Duckit from the command line. If no search term is provided as an argument, you'll be shown the usage.

### Command Line Arguments

* You can provide a search term directly as a command-line argument. For example:
  ```bash
  dotnet run --term "your search term"
  ```

* You can specify a configuration file using the `--config` option. For example:
  ```bash
  dotnet run --config path/to/config.toml --term "quicksort rust"
  ```

* You can enable interactive mode using the `--interactive` option. For example:
  ```bash
  dotnet run --interactive
  ```

* You can specify subtopics using the `--subtopic` option. For example:
  ```bash
  dotnet run --term "how to install arch linux" --subtopic "archwiki"
  ```

## Contributing

Contributions are welcome! Please feel free to submit a pull request or open an issue for any bugs or feature requests.

### Roadmap

- [ ] Unit tests for HTML parsing
- [ ] Implement searching from more search engine (even though for now just DuckDuckGo works well)

## License

This project is licensed under the [Mozilla Public License 2.0](LICENSE).
