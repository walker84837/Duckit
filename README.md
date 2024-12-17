# Search Tool README

## Overview

This project is a simple search tool that queries DuckDuckGo Lite for results based on user-defined search terms. It allows users to specify valid websites to filter results and the maximum number of pages to search. The configuration can be loaded from a file, and if no configuration is found, default values are used.

## Features

- Load configuration from various file formats (YAML, plain text).
- Specify valid websites to filter search results.
- Set a maximum number of pages to search.
- Fallback to default configuration if no config file is found.
- Parse HTML results from DuckDuckGo Lite and display valid links.

## Requirements

- Scala 2.13 or higher
- sbt (Scala Build Tool)
- Dependencies:
  - `sttp.client3` for HTTP requests
  - `Jsoup` for HTML parsing
  - `circe` for JSON and YAML parsing
  - `scala-logging` for logging

## Configuration

The configuration can be specified in a file with the following options:

- `validWebsites`: A comma-separated list of websites to filter results (e.g., `reddit.com,stackoverflow.com`).
- `maxPages`: An integer specifying the maximum number of pages to search.

### Default Configuration

If no configuration file is found, the following default values are used:

- **Valid Websites**: `reddit.com`, `stackoverflow.com`, `stackexchange.com`, `medium.com`
- **Max Pages**: `10`

### Configuration File Locations

The tool will look for configuration files in the following locations (in order):

1. `~/.config/search_tool/config.yml`
2. `~/.config/search_tool/config.yaml`
3. `~/.config/search_tool/config.xml`
4. `~/.config/search_tool/config.txt`
5. `config.yml`
6. `config.yaml`
7. `config.xml`
8. `config.txt`

## Usage

### Running the Tool

You can run the tool from the command line. If no search term is provided as an argument, the tool will prompt you to enter one.

```bash
sbt run
```

### Command Line Arguments

- You can provide a search term directly as command line arguments. For example:

```bash
sbt run "your search term"
```

### Output

The tool will display the search results in the console, showing the title and link of each valid result. If no results are found, a message will indicate that.

## Logging

The tool uses a console logger to output messages regarding the status of the search, including errors and warnings.

## Error Handling

The tool includes error handling for:

- Missing or unreadable configuration files.
- Errors during HTTP requests.
- Parsing errors for configuration files.

In case of errors, the tool will fall back to default configurations and log appropriate messages.

## Contributing

Contributions are welcome! Please feel free to submit a pull request or open an issue for any bugs or feature requests.
