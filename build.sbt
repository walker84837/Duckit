import Dependencies._

ThisBuild / scalaVersion     := "3.3.4"
ThisBuild / version          := "0.1.0-SNAPSHOT"
ThisBuild / organization     := "org.winlogon"
ThisBuild / organizationName := "winlogon"

lazy val root = (project in file("."))
  .settings(
    name := "DuckDuckGoBrowser",
    libraryDependencies += munit % Test
  )

libraryDependencies ++= Seq(
  "com.softwaremill.sttp.client3" %% "core" % "3.9.0",
  "org.jsoup" % "jsoup" % "1.15.4",
  "ch.qos.logback" % "logback-classic" % "1.4.11",
  "com.typesafe.scala-logging" %% "scala-logging" % "3.9.5",
  "io.circe" %% "circe-core" % "0.14.10",
  "io.circe" %% "circe-generic" % "0.14.10",
  "io.circe" %% "circe-parser" % "0.14.10",
  "io.circe" %% "circe-yaml" % "0.16.0"
)
