package com.persistentempires.minecraft.listeners;

import com.persistentempires.minecraft.PersistentEmpiresPlugin;
import org.bukkit.event.EventHandler;
import org.bukkit.event.Listener;
import org.bukkit.event.entity.EntityDamageByEntityEvent;
import org.bukkit.event.entity.EntityDamageEvent;
import org.bukkit.entity.Player;

/**
 * Handles entity damage events
 */
public class EntityDamageListener implements Listener {
    private final PersistentEmpiresPlugin plugin;

    public EntityDamageListener(PersistentEmpiresPlugin plugin) {
        this.plugin = plugin;
    }

    @EventHandler
    public void onEntityDamage(EntityDamageEvent event) {
        if (!(event.getEntity() instanceof Player player)) {
            return;
        }

        // Handle player damage
        plugin.getCombatManager().handlePlayerDamage(player, event.getDamage());
    }

    @EventHandler
    public void onEntityDamageByEntity(EntityDamageByEntityEvent event) {
        if (!(event.getEntity() instanceof Player victim)) {
            return;
        }

        if (!(event.getDamager() instanceof Player attacker)) {
            return;
        }

        // Check for faction PvP rules
        var attackerFaction = plugin.getFactionManager().getPlayerFaction(attacker.getUniqueId().toString());
        var victimFaction = plugin.getFactionManager().getPlayerFaction(victim.getUniqueId().toString());

        // Same faction protection
        if (attackerFaction != null && victimFaction != null && attackerFaction.getId() == victimFaction.getId()) {
            event.setCancelled(true);
            attacker.sendMessage("§cYou cannot attack members of your own faction!");
            return;
        }

        // Check if factions are at war
        if (attackerFaction != null && victimFaction != null) {
            if (!plugin.getFactionManager().areAtWar(attackerFaction.getId(), victimFaction.getId())) {
                event.setCancelled(true);
                attacker.sendMessage("§cYou can only attack factions you are at war with!");
                attacker.sendMessage("§eUse /faction war " + victimFaction.getName() + " to declare war first.");
                return;
            }
        }

        // Apply combat log to both players
        plugin.getCombatManager().handlePlayerDamage(victim, event.getDamage());
        plugin.getPlayerManager().setCombatLog(attacker, plugin.getConfig().getInt("combat.combat_log_duration", 15));
    }
}