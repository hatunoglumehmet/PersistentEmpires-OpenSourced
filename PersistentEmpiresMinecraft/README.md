# Persistent Empires Minecraft Plugin

This is a Minecraft plugin adaptation of the Persistent Empires mod for Mount & Blade II: Bannerlord. It brings the complex faction system, flag capturing mechanics, economy, and RPG elements to Minecraft.

## Features

### Core Systems

1. **Faction System**
   - Create and manage factions with hierarchical roles (Lords, Marshalls)
   - Faction diplomacy with war declarations and peace treaties
   - Faction member management and permissions
   - Faction-based PvP with war requirements

2. **Flag Capturing System**
   - Capture flags to claim territory for your faction
   - Timed capture process with proximity requirements
   - Flag ownership affects territorial control
   - Visual flag displays with faction banners

3. **Armor Weight System**
   - Armor weight affects movement speed
   - Heavier armor provides more protection but reduces mobility
   - Skill-based armor efficiency (planned)

4. **Economy System**
   - Gold-based economy with banking
   - Player-to-player trading
   - Market system for buying/selling items
   - Investment and loan systems
   - Tax system for transactions

5. **Player Persistence**
   - All player data saved to database (SQLite/MySQL)
   - Persistent health, hunger, gold, and equipment
   - Character classes and custom names
   - Experience and skill progression

6. **Combat & Survival**
   - Hunger system affecting health regeneration
   - Combat logging to prevent logout during fights
   - Wounding system for low health
   - Medical items for healing
   - Death penalties and money dropping

7. **Crafting System**
   - Custom crafting recipes with skill requirements
   - Timed crafting process
   - Success rates based on skill level
   - Medieval-themed items and equipment

## Installation

1. **Requirements**
   - Minecraft Server 1.21.3 or higher
   - Java 21 or higher
   - Database (SQLite included, MySQL optional)

2. **Installation Steps**
   - Download the plugin JAR file
   - Place in your server's `plugins` folder
   - Start the server to generate configuration files
   - Configure the database connection in `config.yml`
   - Restart the server

## Configuration

The main configuration file is `config.yml` and includes:

### Database Settings
```yaml
database:
  type: sqlite  # or mysql
  sqlite_path: "data/persistent_empires.db"
  mysql:
    host: localhost
    port: 3306
    database: persistent_empires
    username: root
    password: password
```

### Economy Settings
```yaml
economy:
  bank_limit: 1000000
  starting_gold: 1000
```

### Combat Settings
```yaml
combat:
  combat_log_duration: 15
  death_money_drop_percentage: 25
  medicine_healing_amount: 4
```

### Faction Settings
```yaml
factions:
  max_factions: 20
  creation_cost: 5000
  war_declare_timeout: 30
  peace_declare_timeout: 30
```

### Flag Settings
```yaml
flags:
  capture_duration: 10
  capture_radius: 3
  capture_item: "BANNER"
  capture_cooldown: 60
```

## Commands

### Basic Commands
- `/pe help` - Show help information
- `/pe stats` - Show your character statistics
- `/pe bank deposit <amount>` - Deposit gold to bank
- `/pe bank withdraw <amount>` - Withdraw gold from bank
- `/pe class <class>` - Change your character class
- `/pe name <name>` - Change your display name

### Faction Commands
- `/faction create <name> <displayName>` - Create a faction
- `/faction join <faction>` - Join a faction
- `/faction leave` - Leave your faction
- `/faction info [faction]` - Show faction information
- `/faction list` - List all factions
- `/faction war <faction>` - Declare war on another faction
- `/faction peace <faction>` - Make peace with another faction
- `/faction marshall add <player>` - Add a marshall (lords only)

### Flag Commands
- `/capture start <flag>` - Start capturing a flag
- `/capture cancel` - Cancel current capture
- `/capture info <flag>` - Show flag information
- `/capture list` - List all flags

### Admin Commands
- `/peadmin reload` - Reload configuration
- `/peadmin save` - Save all player data
- `/peadmin player <player> [info|reset|heal]` - Manage players
- `/peadmin faction <faction> [info|delete]` - Manage factions
- `/peadmin flag <flag> [info|delete|tp]` - Manage flags
- `/peadmin economy [give|take] <player> <amount>` - Manage economy

## Permissions

### Basic Permissions
- `persistentempires.use` - Basic plugin usage (default: true)
- `persistentempires.faction.use` - Use faction commands (default: true)
- `persistentempires.capture.use` - Use capture commands (default: true)

### Admin Permissions
- `persistentempires.admin` - Admin access (default: op)
- `persistentempires.faction.create` - Create factions (default: true)
- `persistentempires.faction.manage` - Manage factions (default: false)

## Database Schema

The plugin uses the following database tables:

1. **players** - Player data including stats, inventory, and position
2. **factions** - Faction information and management
3. **flags** - Flag locations and ownership
4. **player_inventory** - Player inventory storage
5. **economy_transactions** - Transaction logs
6. **crafting_recipes** - Custom crafting recipes
7. **faction_wars** - War declarations and status
8. **admin_logs** - Admin action logs

## API Usage

The plugin provides a comprehensive API for other plugins to integrate with:

```java
// Get the plugin instance
PersistentEmpiresPlugin plugin = (PersistentEmpiresPlugin) Bukkit.getPluginManager().getPlugin("PersistentEmpiresMinecraft");

// Access managers
FactionManager factionManager = plugin.getFactionManager();
PlayerManager playerManager = plugin.getPlayerManager();
EconomyManager economyManager = plugin.getEconomyManager();

// Example: Get player faction
PEFaction faction = factionManager.getPlayerFaction(player.getUniqueId().toString());

// Example: Add gold to player
playerManager.addGold(player, 1000);
```

## Performance Considerations

- Database operations are optimized for large player counts
- Player data is cached in memory for fast access
- Periodic auto-save prevents data loss
- Async database operations prevent server lag

## Support

For support, bug reports, or feature requests:
- Create an issue on the GitHub repository
- Join our Discord server
- Check the wiki for detailed documentation

## License

This plugin is open source and released under the MIT License.

## Credits

- Original Persistent Empires mod for Mount & Blade II: Bannerlord
- Minecraft Bukkit/Spigot API
- All contributors and testers