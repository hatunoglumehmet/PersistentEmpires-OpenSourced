package com.persistentempires.minecraft.utils;

import com.persistentempires.minecraft.PersistentEmpiresPlugin;
import org.bukkit.ChatColor;
import org.bukkit.configuration.file.FileConfiguration;

/**
 * Manages configuration and messages for the plugin
 */
public class ConfigManager {
    private final PersistentEmpiresPlugin plugin;
    private final FileConfiguration config;

    public ConfigManager(PersistentEmpiresPlugin plugin) {
        this.plugin = plugin;
        this.config = plugin.getConfig();
    }

    /**
     * Gets a formatted message from the config
     */
    public String getMessage(String key) {
        String prefix = config.getString("messages.prefix", "&6[PE] &r");
        String message = config.getString("messages." + key, "Message not found: " + key);
        
        return ChatColor.translateAlternateColorCodes('&', prefix + message);
    }

    /**
     * Gets a message without the prefix
     */
    public String getMessageNoPrefix(String key) {
        String message = config.getString("messages." + key, "Message not found: " + key);
        return ChatColor.translateAlternateColorCodes('&', message);
    }

    /**
     * Gets a raw message without color formatting
     */
    public String getRawMessage(String key) {
        return config.getString("messages." + key, "Message not found: " + key);
    }

    /**
     * Gets an integer value from config with default
     */
    public int getInt(String path, int defaultValue) {
        return config.getInt(path, defaultValue);
    }

    /**
     * Gets a double value from config with default
     */
    public double getDouble(String path, double defaultValue) {
        return config.getDouble(path, defaultValue);
    }

    /**
     * Gets a boolean value from config with default
     */
    public boolean getBoolean(String path, boolean defaultValue) {
        return config.getBoolean(path, defaultValue);
    }

    /**
     * Gets a string value from config with default
     */
    public String getString(String path, String defaultValue) {
        return config.getString(path, defaultValue);
    }

    /**
     * Reloads the configuration
     */
    public void reloadConfig() {
        plugin.reloadConfig();
    }

    /**
     * Saves the configuration
     */
    public void saveConfig() {
        plugin.saveConfig();
    }

    /**
     * Formats a message with color codes
     */
    public String formatMessage(String message) {
        return ChatColor.translateAlternateColorCodes('&', message);
    }
}