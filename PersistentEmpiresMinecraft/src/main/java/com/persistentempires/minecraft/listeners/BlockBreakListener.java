package com.persistentempires.minecraft.listeners;

import com.persistentempires.minecraft.PersistentEmpiresPlugin;
import org.bukkit.event.EventHandler;
import org.bukkit.event.Listener;
import org.bukkit.event.block.BlockBreakEvent;
import org.bukkit.entity.Player;

/**
 * Handles block break events
 */
public class BlockBreakListener implements Listener {
    private final PersistentEmpiresPlugin plugin;

    public BlockBreakListener(PersistentEmpiresPlugin plugin) {
        this.plugin = plugin;
    }

    @EventHandler
    public void onBlockBreak(BlockBreakEvent event) {
        Player player = event.getPlayer();
        
        // Check if player is in combat
        if (plugin.getCombatManager().isInCombat(player)) {
            event.setCancelled(true);
            player.sendMessage("§cYou cannot break blocks while in combat!");
            return;
        }

        // Check if block is a flag
        var flag = plugin.getFlagManager().getFlagAt(event.getBlock().getLocation());
        if (flag != null) {
            event.setCancelled(true);
            player.sendMessage("§cYou cannot break flags! Use /capture commands instead.");
            return;
        }

        // Check territorial control
        if (plugin.getConfig().getBoolean("world.territorial_control", true)) {
            // Check if player has permission to break blocks in this area
            // This could be expanded to check faction territories, protected areas, etc.
            
            // For now, just allow breaking in most areas
            // You could add more complex territory checking here
        }
    }
}