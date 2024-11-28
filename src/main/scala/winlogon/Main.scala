import sttp.client3._
import org.jsoup.Jsoup
import org.jsoup.nodes.{Document, Element}
import com.typesafe.scalalogging.LazyLogging
import java.net.URLEncoder
import scala.util.Try
import scala.jdk.CollectionConverters._

object Main extends LazyLogging {

  val MaxPages = 10
  val ValidWebsites = List("reddit.com", "stackoverflow.com", "stackexchange.com", "medium.com")

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
    var searchTerm: String = ""

    if (args.length != 1) {
      println("Search query: ")
      searchTerm = scala.io.StdIn.readLine()
    } else {
      searchTerm = args(0)
    }

    var allResults = List.empty[(String, String)]

    for (page <- 0 until MaxPages) {
      val offset = page * MaxPages
      logger.info(s"Starting search for page ${page + 1} with offset $offset")
      val htmlOpt = searchDuckDuckGoLite(searchTerm, offset)

      htmlOpt match {
        case Some(html) =>
          val results = parseResults(html, ValidWebsites)
          if (results.nonEmpty) {
            allResults = allResults ++ results
          } else {
            logger.info(s"No results found on page ${page + 1}")
            // Break if no results are found on the current page
            return
          }
        case None =>
          logger.error("Failed to retrieve HTML; stopping search")
          return
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
