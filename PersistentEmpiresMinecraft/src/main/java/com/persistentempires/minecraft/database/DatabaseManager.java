package com.persistentempires.minecraft.database;

import com.persistentempires.minecraft.PersistentEmpiresPlugin;
import com.persistentempires.minecraft.database.entities.*;
import org.bukkit.configuration.file.FileConfiguration;

import java.io.File;
import java.sql.*;
import java.util.*;
import java.util.logging.Level;

/**
 * Manages database connections and operations for the Persistent Empires plugin
 */
public class DatabaseManager {

    private final PersistentEmpiresPlugin plugin;
    private Connection connection;
    private final String databaseType;
    private final Map<String, String> tableQueries = new HashMap<>();

    public DatabaseManager(PersistentEmpiresPlugin plugin) {
        this.plugin = plugin;
        FileConfiguration config = plugin.getConfig();
        this.databaseType = config.getString("database.type", "sqlite");
        setupTableQueries();
    }

    public boolean initializeDatabase() {
        try {
            if (databaseType.equalsIgnoreCase("sqlite")) {
                initializeSQLite();
            } else if (databaseType.equalsIgnoreCase("mysql")) {
                initializeMySQL();
            } else {
                plugin.getLogger().severe("Invalid database type: " + databaseType);
                return false;
            }

            createTables();
            return true;
        } catch (SQLException e) {
            plugin.getLogger().log(Level.SEVERE, "Failed to initialize database", e);
            return false;
        }
    }

    private void initializeSQLite() throws SQLException {
        String dbPath = plugin.getConfig().getString("database.sqlite_path", "data/persistent_empires.db");
        File dbFile = new File(plugin.getDataFolder(), dbPath);
        
        // Create directory if it doesn't exist
        dbFile.getParentFile().mkdirs();
        
        String url = "jdbc:sqlite:" + dbFile.getAbsolutePath();
        connection = DriverManager.getConnection(url);
        plugin.getLogger().info("Connected to SQLite database: " + dbFile.getAbsolutePath());
    }

    private void initializeMySQL() throws SQLException {
        FileConfiguration config = plugin.getConfig();
        String host = config.getString("database.mysql.host", "localhost");
        int port = config.getInt("database.mysql.port", 3306);
        String database = config.getString("database.mysql.database", "persistent_empires");
        String username = config.getString("database.mysql.username", "root");
        String password = config.getString("database.mysql.password", "password");

        String url = "jdbc:mysql://" + host + ":" + port + "/" + database + "?useSSL=false&serverTimezone=UTC";
        connection = DriverManager.getConnection(url, username, password);
        plugin.getLogger().info("Connected to MySQL database: " + host + ":" + port + "/" + database);
    }

    private void setupTableQueries() {
        // Players table
        tableQueries.put("players", """
            CREATE TABLE IF NOT EXISTS players (
                id INTEGER PRIMARY KEY %s,
                player_uuid VARCHAR(36) UNIQUE NOT NULL,
                player_name VARCHAR(16) NOT NULL,
                faction_id INTEGER DEFAULT 0,
                gold INTEGER DEFAULT 1000,
                bank_amount INTEGER DEFAULT 0,
                hunger INTEGER DEFAULT 100,
                health INTEGER DEFAULT 20,
                experience INTEGER DEFAULT 0,
                class VARCHAR(32) DEFAULT 'peasant',
                pos_x DOUBLE DEFAULT 0,
                pos_y DOUBLE DEFAULT 0,
                pos_z DOUBLE DEFAULT 0,
                world VARCHAR(64) DEFAULT 'world',
                custom_name VARCHAR(32) DEFAULT '',
                last_login BIGINT DEFAULT 0,
                combat_log_until BIGINT DEFAULT 0,
                wounded_until BIGINT DEFAULT 0,
                created_at BIGINT DEFAULT 0,
                updated_at BIGINT DEFAULT 0
            )
            """);

        // Factions table
        tableQueries.put("factions", """
            CREATE TABLE IF NOT EXISTS factions (
                id INTEGER PRIMARY KEY %s,
                name VARCHAR(32) UNIQUE NOT NULL,
                display_name VARCHAR(64) NOT NULL,
                banner_pattern TEXT DEFAULT '',
                lord_uuid VARCHAR(36) DEFAULT '',
                marshalls TEXT DEFAULT '',
                door_managers TEXT DEFAULT '',
                chest_managers TEXT DEFAULT '',
                war_declarations TEXT DEFAULT '',
                poll_unlocked_at BIGINT DEFAULT 0,
                created_at BIGINT DEFAULT 0,
                updated_at BIGINT DEFAULT 0
            )
            """);

        // Flags table
        tableQueries.put("flags", """
            CREATE TABLE IF NOT EXISTS flags (
                id INTEGER PRIMARY KEY %s,
                name VARCHAR(32) UNIQUE NOT NULL,
                world VARCHAR(64) NOT NULL,
                x INTEGER NOT NULL,
                y INTEGER NOT NULL,
                z INTEGER NOT NULL,
                faction_id INTEGER DEFAULT 0,
                capture_radius INTEGER DEFAULT 3,
                capture_duration INTEGER DEFAULT 10,
                last_captured_at BIGINT DEFAULT 0,
                last_captured_by VARCHAR(36) DEFAULT '',
                created_at BIGINT DEFAULT 0,
                updated_at BIGINT DEFAULT 0
            )
            """);

        // Player inventory table
        tableQueries.put("player_inventory", """
            CREATE TABLE IF NOT EXISTS player_inventory (
                id INTEGER PRIMARY KEY %s,
                player_uuid VARCHAR(36) NOT NULL,
                slot_type VARCHAR(16) NOT NULL,
                slot_index INTEGER NOT NULL,
                item_material VARCHAR(64) DEFAULT '',
                item_amount INTEGER DEFAULT 0,
                item_meta TEXT DEFAULT '',
                UNIQUE(player_uuid, slot_type, slot_index)
            )
            """);

        // Economy transactions table
        tableQueries.put("economy_transactions", """
            CREATE TABLE IF NOT EXISTS economy_transactions (
                id INTEGER PRIMARY KEY %s,
                player_uuid VARCHAR(36) NOT NULL,
                transaction_type VARCHAR(16) NOT NULL,
                amount INTEGER NOT NULL,
                description TEXT DEFAULT '',
                timestamp BIGINT DEFAULT 0
            )
            """);

        // Crafting recipes table
        tableQueries.put("crafting_recipes", """
            CREATE TABLE IF NOT EXISTS crafting_recipes (
                id INTEGER PRIMARY KEY %s,
                recipe_name VARCHAR(64) UNIQUE NOT NULL,
                required_items TEXT NOT NULL,
                result_item VARCHAR(64) NOT NULL,
                result_amount INTEGER DEFAULT 1,
                crafting_time INTEGER DEFAULT 30,
                required_level INTEGER DEFAULT 0,
                success_rate INTEGER DEFAULT 100,
                enabled BOOLEAN DEFAULT 1
            )
            """);

        // Faction wars table
        tableQueries.put("faction_wars", """
            CREATE TABLE IF NOT EXISTS faction_wars (
                id INTEGER PRIMARY KEY %s,
                attacker_faction_id INTEGER NOT NULL,
                defender_faction_id INTEGER NOT NULL,
                declared_at BIGINT DEFAULT 0,
                ended_at BIGINT DEFAULT 0,
                status VARCHAR(16) DEFAULT 'active',
                UNIQUE(attacker_faction_id, defender_faction_id)
            )
            """);

        // Admin logs table
        tableQueries.put("admin_logs", """
            CREATE TABLE IF NOT EXISTS admin_logs (
                id INTEGER PRIMARY KEY %s,
                admin_uuid VARCHAR(36) NOT NULL,
                action VARCHAR(32) NOT NULL,
                target_uuid VARCHAR(36) DEFAULT '',
                details TEXT DEFAULT '',
                timestamp BIGINT DEFAULT 0
            )
            """);
    }

    private void createTables() throws SQLException {
        String autoIncrement = databaseType.equalsIgnoreCase("sqlite") ? "AUTOINCREMENT" : "AUTO_INCREMENT";
        
        try (Statement stmt = connection.createStatement()) {
            for (Map.Entry<String, String> entry : tableQueries.entrySet()) {
                String tableName = entry.getKey();
                String query = String.format(entry.getValue(), autoIncrement);
                stmt.execute(query);
                plugin.getLogger().info("Created/verified table: " + tableName);
            }
        }
    }

    public Connection getConnection() {
        return connection;
    }

    public void closeConnections() {
        if (connection != null) {
            try {
                connection.close();
                plugin.getLogger().info("Database connection closed");
            } catch (SQLException e) {
                plugin.getLogger().log(Level.WARNING, "Error closing database connection", e);
            }
        }
    }

    // Player operations
    public void savePlayer(PEPlayer player) {
        String sql = """
            INSERT OR REPLACE INTO players 
            (player_uuid, player_name, faction_id, gold, bank_amount, hunger, health, experience, class, 
             pos_x, pos_y, pos_z, world, custom_name, last_login, combat_log_until, wounded_until, 
             created_at, updated_at) 
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """;

        try (PreparedStatement pstmt = connection.prepareStatement(sql)) {
            pstmt.setString(1, player.getPlayerUUID());
            pstmt.setString(2, player.getPlayerName());
            pstmt.setInt(3, player.getFactionId());
            pstmt.setInt(4, player.getGold());
            pstmt.setInt(5, player.getBankAmount());
            pstmt.setInt(6, player.getHunger());
            pstmt.setInt(7, player.getHealth());
            pstmt.setInt(8, player.getExperience());
            pstmt.setString(9, player.getPlayerClass());
            pstmt.setDouble(10, player.getPosX());
            pstmt.setDouble(11, player.getPosY());
            pstmt.setDouble(12, player.getPosZ());
            pstmt.setString(13, player.getWorld());
            pstmt.setString(14, player.getCustomName());
            pstmt.setLong(15, player.getLastLogin());
            pstmt.setLong(16, player.getCombatLogUntil());
            pstmt.setLong(17, player.getWoundedUntil());
            pstmt.setLong(18, player.getCreatedAt());
            pstmt.setLong(19, System.currentTimeMillis());
            
            pstmt.executeUpdate();
        } catch (SQLException e) {
            plugin.getLogger().log(Level.SEVERE, "Failed to save player: " + player.getPlayerName(), e);
        }
    }

    public PEPlayer loadPlayer(String playerUUID) {
        String sql = "SELECT * FROM players WHERE player_uuid = ?";
        
        try (PreparedStatement pstmt = connection.prepareStatement(sql)) {
            pstmt.setString(1, playerUUID);
            ResultSet rs = pstmt.executeQuery();
            
            if (rs.next()) {
                return new PEPlayer(
                    rs.getString("player_uuid"),
                    rs.getString("player_name"),
                    rs.getInt("faction_id"),
                    rs.getInt("gold"),
                    rs.getInt("bank_amount"),
                    rs.getInt("hunger"),
                    rs.getInt("health"),
                    rs.getInt("experience"),
                    rs.getString("class"),
                    rs.getDouble("pos_x"),
                    rs.getDouble("pos_y"),
                    rs.getDouble("pos_z"),
                    rs.getString("world"),
                    rs.getString("custom_name"),
                    rs.getLong("last_login"),
                    rs.getLong("combat_log_until"),
                    rs.getLong("wounded_until"),
                    rs.getLong("created_at")
                );
            }
        } catch (SQLException e) {
            plugin.getLogger().log(Level.SEVERE, "Failed to load player: " + playerUUID, e);
        }
        
        return null;
    }

    // Faction operations
    public void saveFaction(PEFaction faction) {
        String sql = """
            INSERT OR REPLACE INTO factions 
            (id, name, display_name, banner_pattern, lord_uuid, marshalls, door_managers, chest_managers, 
             war_declarations, poll_unlocked_at, created_at, updated_at) 
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """;

        try (PreparedStatement pstmt = connection.prepareStatement(sql)) {
            pstmt.setInt(1, faction.getId());
            pstmt.setString(2, faction.getName());
            pstmt.setString(3, faction.getDisplayName());
            pstmt.setString(4, faction.getBannerPattern());
            pstmt.setString(5, faction.getLordUUID());
            pstmt.setString(6, String.join("|", faction.getMarshalls()));
            pstmt.setString(7, String.join("|", faction.getDoorManagers()));
            pstmt.setString(8, String.join("|", faction.getChestManagers()));
            pstmt.setString(9, faction.getWarDeclarations().stream()
                .map(String::valueOf)
                .reduce((a, b) -> a + "|" + b)
                .orElse(""));
            pstmt.setLong(10, faction.getPollUnlockedAt());
            pstmt.setLong(11, faction.getCreatedAt());
            pstmt.setLong(12, System.currentTimeMillis());
            
            pstmt.executeUpdate();
        } catch (SQLException e) {
            plugin.getLogger().log(Level.SEVERE, "Failed to save faction: " + faction.getName(), e);
        }
    }

    public List<PEFaction> loadAllFactions() {
        List<PEFaction> factions = new ArrayList<>();
        String sql = "SELECT * FROM factions ORDER BY id";
        
        try (PreparedStatement pstmt = connection.prepareStatement(sql);
             ResultSet rs = pstmt.executeQuery()) {
            
            while (rs.next()) {
                PEFaction faction = new PEFaction(
                    rs.getInt("id"),
                    rs.getString("name"),
                    rs.getString("display_name"),
                    rs.getString("banner_pattern"),
                    rs.getString("lord_uuid"),
                    rs.getLong("poll_unlocked_at"),
                    rs.getLong("created_at")
                );
                
                // Load marshalls
                String marshallsStr = rs.getString("marshalls");
                if (marshallsStr != null && !marshallsStr.isEmpty()) {
                    faction.getMarshalls().addAll(Arrays.asList(marshallsStr.split("\\|")));
                }
                
                // Load door managers
                String doorManagersStr = rs.getString("door_managers");
                if (doorManagersStr != null && !doorManagersStr.isEmpty()) {
                    faction.getDoorManagers().addAll(Arrays.asList(doorManagersStr.split("\\|")));
                }
                
                // Load chest managers
                String chestManagersStr = rs.getString("chest_managers");
                if (chestManagersStr != null && !chestManagersStr.isEmpty()) {
                    faction.getChestManagers().addAll(Arrays.asList(chestManagersStr.split("\\|")));
                }
                
                // Load war declarations
                String warDeclarationsStr = rs.getString("war_declarations");
                if (warDeclarationsStr != null && !warDeclarationsStr.isEmpty()) {
                    for (String warId : warDeclarationsStr.split("\\|")) {
                        try {
                            faction.getWarDeclarations().add(Integer.parseInt(warId));
                        } catch (NumberFormatException e) {
                            // Skip invalid entries
                        }
                    }
                }
                
                factions.add(faction);
            }
        } catch (SQLException e) {
            plugin.getLogger().log(Level.SEVERE, "Failed to load factions", e);
        }
        
        return factions;
    }
}