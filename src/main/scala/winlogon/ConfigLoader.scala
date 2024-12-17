import java.nio.file.{Files, Path, Paths}
import scala.io.Source
import scala.util.{Try, Success, Failure}

/**
  * Configuration class
  * 
  * @param validWebsites List of websites to search
  * @param maxPages Maximum number of pages to search
  * @param fallbackQuery Fallback search query
  * 
  */
case class Config(
  validWebsites: List[String] = Config.DefaultWebsites,
  maxPages: Int = Config.DefaultMaxPages,
  fallbackQuery: Option[String] = None
)

/**
 * Companion object for Config class
 */
object Config {
  val DefaultWebsites: List[String] = List(
    "reddit.com", 
    "stackoverflow.com", 
    "stackexchange.com", 
    "medium.com"
  )
  val DefaultMaxPages: Int = 10

  // Default config file paths
  val DefaultConfigPaths: Seq[Path] = Seq(
    Paths.get(System.getProperty("user.home"), ".config", "search_tool", "config.yml"),
    Paths.get(System.getProperty("user.home"), ".config", "search_tool", "config.yaml"),
    Paths.get(System.getProperty("user.home"), ".config", "search_tool", "config.xml"),
    Paths.get(System.getProperty("user.home"), ".config", "search_tool", "config.txt"),
    Paths.get("config.yml"),
    Paths.get("config.yaml"),
    Paths.get("config.xml"),
    Paths.get("config.txt")
  )
}

/**
  * ConfigLoader class to load configuration from file
  *
  * @param configPaths List of config file paths
  * @param logger Logger
  */
class ConfigLoader(
  configPaths: Seq[Path] = Config.DefaultConfigPaths,
  logger: Logger = new ConsoleLogger()
) {
  // Public method to load configuration
  def load(): Config = {
    findExistingConfigPath(configPaths) match {
      case Some(path) => parseConfigFile(path)
      case None => 
        logger.warn("No config file found; using fallback configuration")
        fallbackConfig()
    }
  }

  // Find first existing config file
  private def findExistingConfigPath(paths: Seq[Path]): Option[Path] = {
    paths.find(Files.exists(_))
  }

  // Parse configuration file based on extension
  private def parseConfigFile(path: Path): Config = {
    try {
      val content = Source.fromFile(path.toFile).mkString
      path.toString.toLowerCase match {
        case p if p.endsWith(".yml") || p.endsWith(".yaml") => parseYAMLConfig(content)
        case _ => parsePlainTextConfig(content)
      }
    } catch {
      case e: Exception =>
        logger.error(s"Error reading config file: ${e.getMessage}")
        fallbackConfig()
    }
  }

  /**
    * YAML-like parsing using circe
    *
    * @param content Content of the config file
    * @return Config object
    */
  private def parseYAMLConfig(content: String): Config = {
    try {
      val websitesLine = content.linesIterator.find(_.contains("validWebsites:"))
      val maxPagesLine = content.linesIterator.find(_.contains("maxPages:"))

      val websites = websitesLine.map(_.split(":").last.trim.split(",").map(_.trim.replace("\"", "")).toList)
        .getOrElse(Config.DefaultWebsites)
      
      val maxPages = maxPagesLine.map(_.split(":").last.trim.toInt)
        .getOrElse(Config.DefaultMaxPages)

      Config(websites, maxPages)
    } catch {
      case e: Exception =>
        logger.error(s"Error parsing YAML-like config: ${e.getMessage}")
        fallbackConfig()
    }
  }

  /**
      * Plain text parsing using basic string manipulation
      *
      * @param content Content of the config file
      * @return Config object
      */
  private def parsePlainTextConfig(content: String): Config = {
    try {
      val lines = content.linesIterator.toList
      val websites = lines.find(_.startsWith("validWebsites="))
        .map(_.split("=").last.split(",").map(_.trim).toList)
        .getOrElse(Config.DefaultWebsites)
      
      val maxPages = lines.find(_.startsWith("maxPages="))
        .flatMap(_.split("=").last.toIntOption)
        .getOrElse(Config.DefaultMaxPages)
      
      Config(websites, maxPages)
    } catch {
      case e: Exception =>
        logger.error(s"Error parsing plain text config: ${e.getMessage}")
        fallbackConfig()
    }
  }

  /**
   * Fallback config if no config file is found
   *
   * @return Config object with default values
   */
  private def fallbackConfig(): Config = {
    Config()
  }
}

/**
  * Companion object for ConfigLoader for creating instances
  * 
  * @param configPaths List of config file paths
  * @param logger Logger
  */
object ConfigLoader {
  def apply(
    configPaths: Seq[Path] = Config.DefaultConfigPaths, 
    logger: Logger = new ConsoleLogger()
  ): ConfigLoader = {
    new ConfigLoader(configPaths, logger)
  }
}
