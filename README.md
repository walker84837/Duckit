# Duckit

## Overview

![duckit](https://github.com/user-attachments/assets/27f0b632-27ef-457f-bf58-bad6596de3c3)

Duckit is a simple search tool that queries DuckDuckGo Lite (or other supported engines in the future) for results based on user-defined search terms. It allows filtering results by specific websites, refining searches with subtopics, and supports interactive and JSON-formatted outputs. The configuration can be loaded from a TOML file, or defaults will be used if no file is provided.

> [!IMPORTANT]
> Duckit may not always work as expected. DuckDuckGo can sometimes ask you to solve a CAPTCHA, even if your traffic appears "legitimate" (non-bot).
>
> Of course, this may temporarily prevent search results from being returned.

## Features

- Load configuration from a TOML file.
- Specify valid websites to filter search results.
- Set a maximum number of results to display.
- Refine searches with subtopics.
- Interactive mode for repeatedly prompting the user for queries.
- JSON-formatted output for easier programmatic consumption.
- Customizable User-Agent headers with built-in presets (Firefox, Chrome, Safari) or custom strings.
- Environment variable `SHOW_HTML` to optionally output raw HTML responses for debugging.

## Configuration

The configuration can be specified in a TOML file with the following options:

* `sites`: A list of websites to filter results (e.g., `["reddit.com", "stackoverflow.com"]`).
* `repl`: Boolean indicating whether to enable interactive mode.
* `search_engine`: A string specifying the search engine to use (`duckduckgo` currently supported).
* `subtopics`: A list of subtopics to refine search results.
* `user_agent`: Optional string to specify the User-Agent. Can use a preset (`"firefox"`, `"chrome"`, `"safari"`) or a custom User-Agent string.

### Example `config.toml`

```toml
[browser]
sites = ["reddit.com", "stackoverflow.com"]
repl = true
search_engine = "duckduckgo"
subtopics = ["quicksort", "jvm"]
user_agent = "firefox"
```

### Default Configuration

If no configuration file is found, these defaults are used:

* **Valid Websites**: None
* **Max Results**: 10
* **Search Engine**: DuckDuckGo
* **Subtopics**: None
* **User-Agent**: Default browser User-Agent (`Mozilla/5.0 ... Firefox`)

## Usage

You can run Duckit from the command line. If no search term is provided as an argument, usage instructions are displayed.

### Command Line Options

* **Search term**:

  ```bash
  dotnet run --term "your search term"
  ```

* **Configuration file**:

  ```bash
  dotnet run --config path/to/config.toml --term "quicksort rust"
  ```

* **Interactive mode**:

  ```bash
  dotnet run --interactive
  ```

  This will run the tool in interactive mode, prompting the user for queries and allowing you to search for more terms without rerunning Duckit.

* **Subtopics**:

  ```bash
  dotnet run --term "how to install arch linux" --subtopic "archwiki"
  ```

* **Links only**:

  ```bash
  dotnet run --term "dotnet tutorials" --links-only
  ```

* **JSON output**:

  ```bash
  dotnet run --term "rust documentation" --json
  ```

* **Custom User-Agent**: Can be set via config file `user_agent` or environment variable (presets: `"firefox"`, `"chrome"`, `"safari"`).

* **Raw HTML output for debugging**: Set `SHOW_HTML` environment variable:

  ```bash
  export SHOW_HTML=true    # Prints HTML to console
  export SHOW_HTML=output.html  # Writes HTML to a file
  ```

## Contributing

Contributions are welcome! Submit pull requests or open issues for bugs or feature requests.

### Roadmap

* [ ] Unit tests for HTML parsing
* [ ] Additional search engines support
* [ ] Configurable themes for interactive output
* [ ] Enhance subtopic search with engine-specific parameters

## License

This project is licensed under the [Mozilla Public License 2.0](LICENSE).
