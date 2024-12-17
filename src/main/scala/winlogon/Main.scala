import sttp.client3._
import org.jsoup.Jsoup
import org.jsoup.nodes.{Document, Element}
import com.typesafe.scalalogging.LazyLogging
import scala.util.{Try, boundary}
import scala.util.control.Breaks._
import scala.jdk.CollectionConverters._
import java.net.URLEncoder
import java.nio.file.{Files, Paths}
import io.circe._
import io.circe.generic.auto._
import io.circe.{parser => jsonParser}
import io.circe.yaml.{parser => yamlParser}

import java.nio.file.StandardOpenOption._

object Main extends LazyLogging {
  def searchDuckDuckGoLite(query: String, offset: Int): Option[String] = {
    val url = "https://lite.duckduckgo.com/lite/"
    val encodedQuery = URLEncoder.encode(query.replace(" ", "+"), "UTF-8")
    val headers = Map(
      "User-Agent" -> "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36"
    )
    val formData = Map(
      "q" -> encodedQuery,
      "s" -> offset.toString
    )

    logger.info(s"Sending POST request to DuckDuckGo Lite with query: $encodedQuery, offset: $offset")

    val backend = HttpURLConnectionBackend()
    val response = basicRequest
      .post(uri"$url")
      .headers(headers)
      .body(formData)
      .send(backend)

    response.body match {
      case Right(html) =>
        logger.info(s"Received response with status code: ${response.code}")
        Some(html)
      case Left(error) =>
        logger.error(s"Failed to fetch results: $error")
        None
    }
  }

  def parseResults(html: String, validWebsites: List[String]): List[(String, String)] = {
    logger.info("Parsing HTML to extract results")
    val doc: Document = Jsoup.parse(html)
    val results: List[(String, String)] = doc.select("a.result-link[rel=nofollow]").asScala.toList.flatMap { link =>
      val href = link.attr("href")
      val title = link.text()
      if (validWebsites.exists(href.contains)) Some(title -> href) else None
    }

    logger.info(s"Found ${results.size} valid results")
    results
  }

  def displayResults(results: List[(String, String)]): Unit = {
    results.foreach { case (title, link) =>
      println(s"\u001B[32m$title\u001B[0m: \u001B[34m$link\u001B[0m")
    }
  }

  def main(args: Array[String]): Unit = {
    val logger = new ConsoleLogger()
    val configLoader = ConfigLoader()
    val config = configLoader.load()

    var searchTerm: String = ""
    if (args.isEmpty) {
      println("Enter your search query:")
      searchTerm = scala.io.StdIn.readLine()
    } else {
      searchTerm = args.mkString(" ")
    }

    val validWebsites = config.validWebsites
    val maxPages = config.maxPages

    var allResults = List.empty[(String, String)]

    for (page <- 0 until maxPages) {
      val offset = page * maxPages
      logger.info(s"Starting search for page ${page + 1} with offset $offset")
      val htmlOpt = searchDuckDuckGoLite(searchTerm, offset)

      htmlOpt match {
        case Some(html) =>
          val results = parseResults(html, validWebsites)
          if (results.nonEmpty) {
            allResults = allResults ++ results
          } else {
            logger.info(s"No results found on page ${page + 1}")
            break()
          }
          displayResults(results)
        case None =>
          logger.error("Failed to retrieve HTML; stopping search")
          break()
      }
    }

    if (allResults.nonEmpty) {
      displayResults(allResults)
    } else {
      logger.info("No results found from the valid websites across all pages")
      println("No results found from the valid websites.")
    }
  }
}
