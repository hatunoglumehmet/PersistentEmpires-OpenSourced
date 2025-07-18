package com.persistentempires.minecraft.listeners;

import com.persistentempires.minecraft.PersistentEmpiresPlugin;
import org.bukkit.event.EventHandler;
import org.bukkit.event.Listener;
import org.bukkit.event.block.BlockPlaceEvent;
import org.bukkit.entity.Player;

/**
 * Handles block place events
 */
public class BlockPlaceListener implements Listener {
    private final PersistentEmpiresPlugin plugin;

    public BlockPlaceListener(PersistentEmpiresPlugin plugin) {
        this.plugin = plugin;
    }

    @EventHandler
    public void onBlockPlace(BlockPlaceEvent event) {
        Player player = event.getPlayer();
        
        // Check if player is in combat
        if (plugin.getCombatManager().isInCombat(player)) {
            event.setCancelled(true);
            player.sendMessage("§cYou cannot place blocks while in combat!");
            return;
        }

        // Check territorial control
        if (plugin.getConfig().getBoolean("world.territorial_control", true)) {
            // Check if player has permission to place blocks in this area
            // This could be expanded to check faction territories, protected areas, etc.
            
            // For now, just allow placing in most areas
            // You could add more complex territory checking here
        }

        // Check spawn protection
        int spawnProtection = plugin.getConfig().getInt("world.spawn_protection_radius", 50);
        if (spawnProtection > 0) {
            org.bukkit.Location spawn = event.getBlock().getWorld().getSpawnLocation();
            if (event.getBlock().getLocation().distance(spawn) <= spawnProtection) {
                if (!player.hasPermission("persistentempires.admin")) {
                    event.setCancelled(true);
                    player.sendMessage("§cYou cannot place blocks near spawn!");
                    return;
                }
            }
        }
    }
}