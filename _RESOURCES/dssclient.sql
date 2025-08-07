-- --------------------------------------------------------
-- Host:                         127.0.0.1
-- Server version:               11.8.2-MariaDB - mariadb.org binary distribution
-- Server OS:                    Win64
-- HeidiSQL Version:             12.10.0.7000
-- --------------------------------------------------------

/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET NAMES utf8 */;
/*!50503 SET NAMES utf8mb4 */;
/*!40103 SET @OLD_TIME_ZONE=@@TIME_ZONE */;
/*!40103 SET TIME_ZONE='+00:00' */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;
/*!40111 SET @OLD_SQL_NOTES=@@SQL_NOTES, SQL_NOTES=0 */;


-- Dumping database structure for dss_client
CREATE DATABASE IF NOT EXISTS `dss_client` /*!40100 DEFAULT CHARACTER SET latin1 COLLATE latin1_swedish_ci */;
USE `dss_client`;

-- Dumping structure for table dss_client.directory
CREATE TABLE IF NOT EXISTS `directory` (
  `workspace` bigint(20) NOT NULL,
  `parent` bigint(20) NOT NULL DEFAULT 0 COMMENT 'Can be null for root folders. We mark it as 0 for root folders',
  `name` varchar(120) NOT NULL,
  `id` bigint(20) NOT NULL AUTO_INCREMENT,
  `display_name` varchar(120) NOT NULL,
  `created` timestamp NOT NULL DEFAULT current_timestamp(),
  `modified` timestamp NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  `guid` varchar(48) NOT NULL DEFAULT 'uuid()',
  `deleted` bit(1) NOT NULL DEFAULT b'0' COMMENT 'soft delete',
  PRIMARY KEY (`id`),
  UNIQUE KEY `unq_directory` (`workspace`,`parent`,`name`),
  UNIQUE KEY `unq_directory_0` (`guid`),
  CONSTRAINT `fk_directory_workspace` FOREIGN KEY (`workspace`) REFERENCES `workspace` (`id`) ON DELETE NO ACTION ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

-- Data exporting was unselected.

-- Dumping structure for table dss_client.document
CREATE TABLE IF NOT EXISTS `document` (
  `id` bigint(20) NOT NULL AUTO_INCREMENT,
  `workspace` bigint(20) NOT NULL,
  `dir` bigint(20) NOT NULL,
  `name` varchar(200) NOT NULL,
  `cuid` varchar(48) NOT NULL DEFAULT 'uuid()' COMMENT 'Collision Resistant Global unique identifier',
  `created` timestamp NOT NULL DEFAULT current_timestamp(),
  `modified` timestamp NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  `deleted` bit(1) NOT NULL DEFAULT b'0' COMMENT 'Soft delete',
  PRIMARY KEY (`id`),
  UNIQUE KEY `unq_file_index` (`cuid`),
  UNIQUE KEY `unq_document` (`workspace`,`dir`,`name`),
  KEY `fk_file_index_parent` (`workspace`),
  KEY `fk_document_directory` (`dir`),
  CONSTRAINT `fk_document_directory` FOREIGN KEY (`dir`) REFERENCES `directory` (`id`) ON DELETE NO ACTION ON UPDATE NO ACTION,
  CONSTRAINT `fk_document_workspace` FOREIGN KEY (`workspace`) REFERENCES `workspace` (`id`) ON DELETE NO ACTION ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

-- Data exporting was unselected.

-- Dumping structure for table dss_client.doc_info
CREATE TABLE IF NOT EXISTS `doc_info` (
  `extension` int(11) DEFAULT NULL,
  `file` bigint(20) NOT NULL,
  `display_name` varchar(200) NOT NULL,
  `saveas_name` varchar(200) NOT NULL,
  `path` text DEFAULT NULL COMMENT 'cached for performance',
  `valid` int(11) DEFAULT NULL,
  PRIMARY KEY (`file`),
  CONSTRAINT `fk_file_info_file_index_0` FOREIGN KEY (`file`) REFERENCES `document` (`id`) ON DELETE NO ACTION ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

-- Data exporting was unselected.

-- Dumping structure for table dss_client.doc_version
CREATE TABLE IF NOT EXISTS `doc_version` (
  `id` bigint(20) NOT NULL AUTO_INCREMENT,
  `created` timestamp NOT NULL DEFAULT current_timestamp(),
  `size` bigint(20) NOT NULL COMMENT 'in bytes',
  `version` int(11) NOT NULL DEFAULT 1,
  `doc` bigint(20) NOT NULL,
  PRIMARY KEY (`id`),
  KEY `idx_file_version` (`doc`,`version`),
  KEY `idx_file_version_0` (`created`),
  CONSTRAINT `fk_file_version_file_index` FOREIGN KEY (`doc`) REFERENCES `document` (`id`) ON DELETE NO ACTION ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

-- Data exporting was unselected.

-- Dumping structure for table dss_client.extension
CREATE TABLE IF NOT EXISTS `extension` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `name` varchar(100) NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

-- Data exporting was unselected.

-- Dumping structure for table dss_client.workspace
CREATE TABLE IF NOT EXISTS `workspace` (
  `id` bigint(20) NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

-- Data exporting was unselected.

/*!40103 SET TIME_ZONE=IFNULL(@OLD_TIME_ZONE, 'system') */;
/*!40101 SET SQL_MODE=IFNULL(@OLD_SQL_MODE, '') */;
/*!40014 SET FOREIGN_KEY_CHECKS=IFNULL(@OLD_FOREIGN_KEY_CHECKS, 1) */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40111 SET SQL_NOTES=IFNULL(@OLD_SQL_NOTES, 1) */;
