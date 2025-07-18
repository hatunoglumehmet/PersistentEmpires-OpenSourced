package com.persistentempires.minecraft.listeners;

import com.persistentempires.minecraft.PersistentEmpiresPlugin;
import org.bukkit.event.EventHandler;
import org.bukkit.event.Listener;
import org.bukkit.event.player.PlayerJoinEvent;

/**
 * Handles player join events
 */
public class PlayerJoinListener implements Listener {
    private final PersistentEmpiresPlugin plugin;

    public PlayerJoinListener(PersistentEmpiresPlugin plugin) {
        this.plugin = plugin;
    }

    @EventHandler
    public void onPlayerJoin(PlayerJoinEvent event) {
        // Load player data
        plugin.getPlayerManager().loadPlayer(event.getPlayer());
        
        // Update movement speed based on armor
        plugin.getCombatManager().updateMovementSpeed(event.getPlayer());
        
        // Send welcome message
        event.getPlayer().sendMessage("§6Welcome to Persistent Empires!");
        event.getPlayer().sendMessage("§eUse /pe help for commands and /faction help for faction commands.");
    }
}