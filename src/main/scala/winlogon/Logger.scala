/**
  * Logger trait
  */
trait Logger {
  def error(message: String): Unit
  def warn(message: String): Unit
  def info(message: String): Unit
}

/**
  * Console logger
  *
  * Prints messages to console
  */
class ConsoleLogger extends Logger {
  def error(message: String): Unit = System.err.println(s"ERROR: $message")
  def warn(message: String): Unit = println(s"WARN: $message")
  def info(message: String): Unit = println(s"INFO: $message")
}
