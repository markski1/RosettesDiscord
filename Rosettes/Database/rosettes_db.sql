/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET NAMES utf8 */;
/*!50503 SET NAMES utf8mb4 */;
/*!40103 SET @OLD_TIME_ZONE=@@TIME_ZONE */;
/*!40103 SET TIME_ZONE='+00:00' */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;
/*!40111 SET @OLD_SQL_NOTES=@@SQL_NOTES, SQL_NOTES=0 */;

-- Volcando estructura para tabla bot_db.alarms
CREATE TABLE `alarms` (
  `id` INT(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `datetime` DATETIME NOT NULL,
  `user` BIGINT(20) UNSIGNED NOT NULL DEFAULT '0',
  `channel` BIGINT(20) UNSIGNED NOT NULL DEFAULT '0',
  `message` VARCHAR(256) NOT NULL DEFAULT '' COLLATE 'utf8mb4_unicode_ci',
  PRIMARY KEY (`id`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- La exportación de datos fue deseleccionada.

-- Volcando estructura para tabla bot_db.app_auth
CREATE TABLE IF NOT EXISTS `app_auth` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `name` varchar(50) NOT NULL DEFAULT '0',
  `owner_id` bigint(20) unsigned NOT NULL DEFAULT 0,
  `key` varchar(50) NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`),
  KEY `key` (`key`)
) ENGINE=InnoDB AUTO_INCREMENT=2 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='Authorized applications';

-- La exportación de datos fue deseleccionada.

-- Volcando estructura para tabla bot_db.app_auth_rel
CREATE TABLE IF NOT EXISTS `app_auth_rel` (
  `user_id` bigint(20) NOT NULL,
  `app_id` int(11) NOT NULL,
  `created_at` datetime NOT NULL DEFAULT current_timestamp(),
  PRIMARY KEY (`user_id`,`app_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- La exportación de datos fue deseleccionada.

-- Volcando estructura para tabla bot_db.autorole_entries
CREATE TABLE IF NOT EXISTS `autorole_entries` (
  `guildid` bigint(20) unsigned NOT NULL DEFAULT 0,
  `emote` varchar(10) NOT NULL DEFAULT '0',
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
  `name` varchar(32) DEFAULT 'Autoroles!',
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=16 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- La exportación de datos fue deseleccionada.

-- Volcando estructura para tabla bot_db.custom_cmds
CREATE TABLE IF NOT EXISTS `custom_cmds` (
  `guildid` bigint(20) unsigned NOT NULL DEFAULT 0,
  `name` varchar(20) NOT NULL DEFAULT 'a',
  `description` varchar(50) NOT NULL DEFAULT 'Empty',
  `ephemeral` int(11) NOT NULL DEFAULT 0,
  `action` int(11) NOT NULL DEFAULT 0,
  `value` text NOT NULL DEFAULT 'empty',
  PRIMARY KEY (`name`,`guildid`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- La exportación de datos fue deseleccionada.

-- Volcando estructura para tabla bot_db.guilds
CREATE TABLE IF NOT EXISTS `guilds` (
  `id` bigint(20) unsigned NOT NULL DEFAULT 0,
  `namecache` varchar(50) NOT NULL DEFAULT 'rosettes_unset',
  `members` bigint(20) unsigned NOT NULL DEFAULT 0,
  `settings` varchar(10) NOT NULL DEFAULT '1111111111',
  `ownerid` bigint(20) unsigned NOT NULL DEFAULT 0,
  `defaultrole` bigint(20) unsigned NOT NULL DEFAULT 0,
  `logchan` bigint(20) unsigned NOT NULL DEFAULT 0,
  `rpgchan` bigint(20) unsigned NOT NULL DEFAULT 0,
  `telemetry` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_bin DEFAULT NULL CHECK (json_valid(`telemetry`)),
  `created_at` datetime NOT NULL DEFAULT current_timestamp(),
  PRIMARY KEY (`id`),
  UNIQUE KEY `id` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- La exportación de datos fue deseleccionada.

-- Volcando estructura para tabla bot_db.login_keys
CREATE TABLE IF NOT EXISTS `login_keys` (
  `id` bigint(20) unsigned NOT NULL DEFAULT 0,
  `login_key` varchar(64) NOT NULL DEFAULT 'NO',
  PRIMARY KEY (`id`),
  UNIQUE KEY `key` (`login_key`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- La exportación de datos fue deseleccionada.

-- Volcando estructura para tabla bot_db.pets
CREATE TABLE IF NOT EXISTS `pets` (
  `pet_id` int(11) NOT NULL AUTO_INCREMENT,
  `pet_index` int(11) DEFAULT NULL,
  `owner_id` bigint(20) unsigned DEFAULT NULL,
  `pet_name` varchar(25) DEFAULT NULL,
  `exp` int(11) DEFAULT 0,
  `times_pet` int(11) DEFAULT 0,
  `happiness` int(11) DEFAULT 100,
  `found_date` int(11) DEFAULT 0,
  PRIMARY KEY (`pet_id`)
) ENGINE=InnoDB AUTO_INCREMENT=159 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- La exportación de datos fue deseleccionada.

-- Volcando estructura para tabla bot_db.polls
CREATE TABLE IF NOT EXISTS `polls` (
  `id` bigint(20) unsigned NOT NULL,
  `question` varchar(256) NOT NULL DEFAULT '',
  `option1` varchar(128) NOT NULL DEFAULT '',
  `option2` varchar(128) NOT NULL DEFAULT '',
  `option3` varchar(128) NOT NULL DEFAULT '',
  `option4` varchar(128) NOT NULL DEFAULT '',
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
  `relevantstringvalue` text NOT NULL DEFAULT 'none'
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- La exportación de datos fue deseleccionada.

-- Volcando estructura para tabla bot_db.roles
CREATE TABLE IF NOT EXISTS `roles` (
  `id` bigint(20) unsigned NOT NULL DEFAULT 0,
  `rolename` varchar(64) NOT NULL DEFAULT '0',
  `color` varchar(10) NOT NULL,
  `guildid` bigint(20) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  KEY `guildid` (`guildid`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- La exportación de datos fue deseleccionada.

-- Volcando estructura para tabla bot_db.telemetry
CREATE TABLE IF NOT EXISTS `telemetry` (
  `time` datetime DEFAULT current_timestamp(),
  `cmd_count` int(11) DEFAULT NULL,
  `interaction_count` int(11) DEFAULT NULL,
  `message_count` int(11) DEFAULT NULL,
  `count_by_command` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_bin DEFAULT NULL CHECK (json_valid(`count_by_command`)),
  KEY `time` (`time`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- La exportación de datos fue deseleccionada.

-- Volcando estructura para tabla bot_db.users
CREATE TABLE IF NOT EXISTS `users` (
  `id` bigint(20) unsigned NOT NULL DEFAULT 0,
  `username` varchar(50) NOT NULL DEFAULT 'rosettes_unset',
  `namecache` varchar(50) NOT NULL DEFAULT 'rosettes_unset',
  `exp` int(11) NOT NULL DEFAULT 0,
  `mainpet` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  UNIQUE KEY `id` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- La exportación de datos fue deseleccionada.

-- Volcando estructura para tabla bot_db.users_crops
CREATE TABLE IF NOT EXISTS `users_crops` (
  `plot_id` int(11) NOT NULL,
  `user_id` bigint(20) unsigned NOT NULL,
  `unix_growth` int(10) DEFAULT NULL,
  `unix_next_water` int(10) DEFAULT NULL,
  `crop_type` int(10) DEFAULT NULL,
  PRIMARY KEY (`user_id`,`plot_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- La exportación de datos fue deseleccionada.

-- Volcando estructura para tabla bot_db.users_inventory
CREATE TABLE IF NOT EXISTS `users_inventory` (
  `id` bigint(20) unsigned NOT NULL,
  `dabloons` int(11) NOT NULL DEFAULT 10,
  `fish` int(11) NOT NULL DEFAULT 0,
  `uncommonfish` int(11) NOT NULL DEFAULT 0,
  `rarefish` int(11) NOT NULL DEFAULT 0,
  `shrimp` int(11) NOT NULL DEFAULT 0,
  `garbage` int(11) NOT NULL DEFAULT 0,
  `pets` varchar(31) NOT NULL DEFAULT '000000000000000000000000000000' COMMENT 'Where each digit represents if the user has a given pet or not.',
  `plots` int(10) NOT NULL DEFAULT 1,
  `tomato` int(11) NOT NULL DEFAULT 0,
  `carrot` int(11) NOT NULL DEFAULT 0,
  `potato` int(11) NOT NULL DEFAULT 0,
  `seedbag` int(11) NOT NULL DEFAULT 0,
  `fishpole` int(11) NOT NULL DEFAULT 50,
  `farmtools` int(11) NOT NULL DEFAULT 50,
  PRIMARY KEY (`id`) USING BTREE,
  UNIQUE KEY `id` (`id`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci ROW_FORMAT=DYNAMIC;

-- La exportación de datos fue deseleccionada.

/*!40103 SET TIME_ZONE=IFNULL(@OLD_TIME_ZONE, 'system') */;
/*!40101 SET SQL_MODE=IFNULL(@OLD_SQL_MODE, '') */;
/*!40014 SET FOREIGN_KEY_CHECKS=IFNULL(@OLD_FOREIGN_KEY_CHECKS, 1) */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40111 SET SQL_NOTES=IFNULL(@OLD_SQL_NOTES, 1) */;
