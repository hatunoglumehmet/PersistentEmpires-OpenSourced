package com.persistentempires.minecraft.listeners;

import com.persistentempires.minecraft.PersistentEmpiresPlugin;
import org.bukkit.event.EventHandler;
import org.bukkit.event.Listener;
import org.bukkit.event.player.PlayerDeathEvent;

/**
 * Handles player death events
 */
public class PlayerDeathListener implements Listener {
    private final PersistentEmpiresPlugin plugin;

    public PlayerDeathListener(PersistentEmpiresPlugin plugin) {
        this.plugin = plugin;
    }

    @EventHandler
    public void onPlayerDeath(PlayerDeathEvent event) {
        // Handle death penalties and effects
        plugin.getCombatManager().handlePlayerDeath(event.getEntity());
        
        // Cancel any active flag capture
        plugin.getFlagManager().cancelCapture(event.getEntity());
        
        // Cancel any active crafting
        plugin.getCraftingManager().cancelCrafting(event.getEntity());
        
        // Custom death message based on faction
        var faction = plugin.getFactionManager().getPlayerFaction(event.getEntity().getUniqueId().toString());
        if (faction != null) {
            event.setDeathMessage("§c" + event.getEntity().getName() + " §7of §e" + faction.getDisplayName() + " §7has fallen!");
        } else {
            event.setDeathMessage("§c" + event.getEntity().getName() + " §7has fallen!");
        }
    }
}