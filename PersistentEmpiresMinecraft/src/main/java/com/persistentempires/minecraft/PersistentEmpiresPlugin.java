package com.persistentempires.minecraft;

import com.persistentempires.minecraft.commands.*;
import com.persistentempires.minecraft.database.DatabaseManager;
import com.persistentempires.minecraft.factions.FactionManager;
import com.persistentempires.minecraft.flags.FlagManager;
import com.persistentempires.minecraft.listeners.*;
import com.persistentempires.minecraft.managers.*;
import com.persistentempires.minecraft.utils.ConfigManager;
import org.bukkit.plugin.java.JavaPlugin;

import java.util.Objects;
import java.util.logging.Level;

/**
 * Main plugin class for Persistent Empires Minecraft
 * Provides faction system, flag capturing, armor weight mechanics, and RPG elements
 */
public class PersistentEmpiresPlugin extends JavaPlugin {

    // Managers
    private DatabaseManager databaseManager;
    private FactionManager factionManager;
    private FlagManager flagManager;
    private PlayerManager playerManager;
    private EconomyManager economyManager;
    private CraftingManager craftingManager;
    private CombatManager combatManager;
    private ConfigManager configManager;

    @Override
    public void onEnable() {
        getLogger().info("Enabling Persistent Empires Minecraft...");
        
        // Initialize configuration
        saveDefaultConfig();
        configManager = new ConfigManager(this);
        
        // Initialize database
        try {
            databaseManager = new DatabaseManager(this);
            if (!databaseManager.initializeDatabase()) {
                getLogger().severe("Failed to initialize database! Disabling plugin.");
                getServer().getPluginManager().disablePlugin(this);
                return;
            }
        } catch (Exception e) {
            getLogger().log(Level.SEVERE, "Error initializing database", e);
            getServer().getPluginManager().disablePlugin(this);
            return;
        }
        
        // Initialize managers
        initializeManagers();
        
        // Register commands
        registerCommands();
        
        // Register listeners
        registerListeners();
        
        // Start periodic tasks
        startPeriodicTasks();
        
        getLogger().info("Persistent Empires Minecraft enabled successfully!");
    }

    @Override
    public void onDisable() {
        getLogger().info("Disabling Persistent Empires Minecraft...");
        
        // Save all player data
        if (playerManager != null) {
            playerManager.saveAllPlayers();
        }
        
        // Close database connections
        if (databaseManager != null) {
            databaseManager.closeConnections();
        }
        
        getLogger().info("Persistent Empires Minecraft disabled successfully!");
    }

    private void initializeManagers() {
        factionManager = new FactionManager(this);
        flagManager = new FlagManager(this);
        playerManager = new PlayerManager(this);
        economyManager = new EconomyManager(this);
        craftingManager = new CraftingManager(this);
        combatManager = new CombatManager(this);
        
        getLogger().info("All managers initialized successfully!");
    }

    private void registerCommands() {
        Objects.requireNonNull(getCommand("pe")).setExecutor(new PersistentEmpiresCommand(this));
        Objects.requireNonNull(getCommand("faction")).setExecutor(new FactionCommand(this));
        Objects.requireNonNull(getCommand("capture")).setExecutor(new CaptureCommand(this));
        Objects.requireNonNull(getCommand("peadmin")).setExecutor(new AdminCommand(this));
        
        getLogger().info("Commands registered successfully!");
    }

    private void registerListeners() {
        getServer().getPluginManager().registerEvents(new PlayerJoinListener(this), this);
        getServer().getPluginManager().registerEvents(new PlayerQuitListener(this), this);
        getServer().getPluginManager().registerEvents(new PlayerDeathListener(this), this);
        getServer().getPluginManager().registerEvents(new PlayerInteractListener(this), this);
        getServer().getPluginManager().registerEvents(new PlayerMoveListener(this), this);
        getServer().getPluginManager().registerEvents(new EntityDamageListener(this), this);
        getServer().getPluginManager().registerEvents(new BlockBreakListener(this), this);
        getServer().getPluginManager().registerEvents(new BlockPlaceListener(this), this);
        getServer().getPluginManager().registerEvents(new InventoryListener(this), this);
        
        getLogger().info("Event listeners registered successfully!");
    }

    private void startPeriodicTasks() {
        // Auto-save task every 5 minutes
        getServer().getScheduler().runTaskTimerAsynchronously(this, () -> {
            if (playerManager != null) {
                playerManager.saveAllPlayers();
            }
        }, 6000L, 6000L); // 5 minutes in ticks
        
        // Hunger and health regeneration task every 30 seconds
        getServer().getScheduler().runTaskTimer(this, () -> {
            if (combatManager != null) {
                combatManager.processHungerAndHealth();
            }
        }, 600L, 600L); // 30 seconds in ticks
        
        // Faction war cooldown task every minute
        getServer().getScheduler().runTaskTimer(this, () -> {
            if (factionManager != null) {
                factionManager.processWarCooldowns();
            }
        }, 1200L, 1200L); // 1 minute in ticks
        
        getLogger().info("Periodic tasks started successfully!");
    }

    // Getters for managers
    public DatabaseManager getDatabaseManager() { return databaseManager; }
    public FactionManager getFactionManager() { return factionManager; }
    public FlagManager getFlagManager() { return flagManager; }
    public PlayerManager getPlayerManager() { return playerManager; }
    public EconomyManager getEconomyManager() { return economyManager; }
    public CraftingManager getCraftingManager() { return craftingManager; }
    public CombatManager getCombatManager() { return combatManager; }
    public ConfigManager getConfigManager() { return configManager; }
}