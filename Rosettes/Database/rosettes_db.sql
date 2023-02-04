-- --------------------------------------------------------
-- Host:                         gateway.markski.ar
-- Versión del servidor:         10.6.7-MariaDB-1:10.6.7+maria~focal - mariadb.org binary distribution
-- SO del servidor:              debian-linux-gnu
-- HeidiSQL Versión:             12.0.0.6468
-- --------------------------------------------------------

/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET NAMES utf8 */;
/*!50503 SET NAMES utf8mb4 */;
/*!40103 SET @OLD_TIME_ZONE=@@TIME_ZONE */;
/*!40103 SET TIME_ZONE='+00:00' */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;
/*!40111 SET @OLD_SQL_NOTES=@@SQL_NOTES, SQL_NOTES=0 */;


-- Volcando estructura de base de datos para bot_db
CREATE DATABASE IF NOT EXISTS `bot_db` /*!40100 DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci */;
USE `bot_db`;

-- Volcando estructura para tabla bot_db.alarms
CREATE TABLE IF NOT EXISTS `alarms` (
  `datetime` datetime NOT NULL,
  `user` bigint(20) unsigned NOT NULL DEFAULT 0,
  `channel` bigint(20) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`user`),
  UNIQUE KEY `user` (`user`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- La exportación de datos fue deseleccionada.

-- Volcando estructura para tabla bot_db.autorole_entries
CREATE TABLE IF NOT EXISTS `autorole_entries` (
  `guildid` bigint(20) unsigned NOT NULL DEFAULT 0,
  `emote` varchar(10) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '0',
  `roleid` bigint(20) unsigned NOT NULL DEFAULT 0,
  `rolegroupid` int(10) unsigned NOT NULL DEFAULT 0,
  KEY `guildid` (`guildid`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- La exportación de datos fue deseleccionada.

-- Volcando estructura para tabla bot_db.autorole_groups
CREATE TABLE IF NOT EXISTS `autorole_groups` (
  `id` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `guildid` bigint(20) unsigned NOT NULL DEFAULT 0,
  `messageid` bigint(20) unsigned NOT NULL DEFAULT 0,
  `name` varchar(32) COLLATE utf8mb4_unicode_ci DEFAULT 'Autoroles!',
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=15 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- La exportación de datos fue deseleccionada.

-- Volcando estructura para tabla bot_db.guilds
CREATE TABLE IF NOT EXISTS `guilds` (
  `id` bigint(20) unsigned NOT NULL DEFAULT 0,
  `namecache` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'rosettes_unset',
  `members` bigint(20) unsigned NOT NULL DEFAULT 0,
  `settings` varchar(10) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '1111111111',
  `ownerid` bigint(20) unsigned NOT NULL DEFAULT 0,
  `defaultrole` bigint(20) unsigned NOT NULL DEFAULT 0,
  `logchan` bigint(20) unsigned NOT NULL DEFAULT 0,
  `rpgchan` bigint(20) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  UNIQUE KEY `id` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- La exportación de datos fue deseleccionada.

-- Volcando estructura para tabla bot_db.login_keys
CREATE TABLE IF NOT EXISTS `login_keys` (
  `id` bigint(20) unsigned NOT NULL DEFAULT 0,
  `login_key` varchar(64) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'NO',
  PRIMARY KEY (`id`),
  UNIQUE KEY `key` (`login_key`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- La exportación de datos fue deseleccionada.

-- Volcando estructura para tabla bot_db.polls
CREATE TABLE IF NOT EXISTS `polls` (
  `id` bigint(20) unsigned NOT NULL,
  `question` varchar(256) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `option1` varchar(128) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `option2` varchar(128) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `option3` varchar(128) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `option4` varchar(128) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `count1` int(10) unsigned NOT NULL DEFAULT 0,
  `count2` int(10) unsigned NOT NULL DEFAULT 0,
  `count3` int(10) unsigned NOT NULL DEFAULT 0,
  `count4` int(10) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- La exportación de datos fue deseleccionada.

-- Volcando estructura para tabla bot_db.poll_votes
CREATE TABLE IF NOT EXISTS `poll_votes` (
  `user_id` bigint(20) unsigned NOT NULL DEFAULT 0,
  `poll_id` bigint(20) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`user_id`,`poll_id`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- La exportación de datos fue deseleccionada.

-- Volcando estructura para tabla bot_db.requests
CREATE TABLE IF NOT EXISTS `requests` (
  `requesttype` int(10) unsigned NOT NULL DEFAULT 0,
  `relevantguild` bigint(20) unsigned NOT NULL DEFAULT 0,
  `relevantvalue` bigint(20) unsigned NOT NULL DEFAULT 0,
  `relevantstringvalue` text COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'none'
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- La exportación de datos fue deseleccionada.

-- Volcando estructura para tabla bot_db.roles
CREATE TABLE IF NOT EXISTS `roles` (
  `id` bigint(20) unsigned NOT NULL DEFAULT 0,
  `rolename` varchar(64) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '0',
  `color` varchar(10) COLLATE utf8mb4_unicode_ci NOT NULL,
  `guildid` bigint(20) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  KEY `guildid` (`guildid`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- La exportación de datos fue deseleccionada.

-- Volcando estructura para tabla bot_db.users
CREATE TABLE IF NOT EXISTS `users` (
  `id` bigint(20) unsigned NOT NULL DEFAULT 0,
  `namecache` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'rosettes_unset',
  `exp` int(11) NOT NULL DEFAULT 0,
  `mainpet` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  UNIQUE KEY `id` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- La exportación de datos fue deseleccionada.

-- Volcando estructura para tabla bot_db.users_inventory
CREATE TABLE IF NOT EXISTS `users_inventory` (
  `id` bigint(20) unsigned NOT NULL DEFAULT 0,
  `dabloons` int(11) NOT NULL DEFAULT 10,
  `fish` int(11) NOT NULL DEFAULT 0,
  `uncommonfish` int(11) NOT NULL DEFAULT 0,
  `rarefish` int(11) NOT NULL DEFAULT 0,
  `shrimp` int(11) NOT NULL DEFAULT 0,
  `rice` int(11) NOT NULL DEFAULT 0,
  `garbage` int(11) NOT NULL DEFAULT 0,
  `sushi` int(11) NOT NULL DEFAULT 0,
  `shrimprice` int(11) NOT NULL DEFAULT 0,
  `pets` varchar(21) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '00000000000000000000' COMMENT 'Where each digit represents if the user has a given pet or not.',
  PRIMARY KEY (`id`) USING BTREE,
  UNIQUE KEY `id` (`id`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci ROW_FORMAT=DYNAMIC;

-- La exportación de datos fue deseleccionada.

/*!40103 SET TIME_ZONE=IFNULL(@OLD_TIME_ZONE, 'system') */;
/*!40101 SET SQL_MODE=IFNULL(@OLD_SQL_MODE, '') */;
/*!40014 SET FOREIGN_KEY_CHECKS=IFNULL(@OLD_FOREIGN_KEY_CHECKS, 1) */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40111 SET SQL_NOTES=IFNULL(@OLD_SQL_NOTES, 1) */;
