package com.persistentempires.minecraft.factions;

import com.persistentempires.minecraft.PersistentEmpiresPlugin;
import com.persistentempires.minecraft.database.entities.PEFaction;
import com.persistentempires.minecraft.database.entities.PEPlayer;
import org.bukkit.entity.Player;

import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;

/**
 * Manages factions, including creation, membership, diplomacy, and hierarchy
 */
public class FactionManager {
    private final PersistentEmpiresPlugin plugin;
    private final Map<Integer, PEFaction> factions = new ConcurrentHashMap<>();
    private final Map<String, Integer> playerFactions = new ConcurrentHashMap<>();
    private final Map<Integer, Long> warCooldowns = new ConcurrentHashMap<>();
    private final Map<Integer, Long> peaceCooldowns = new ConcurrentHashMap<>();
    private int nextFactionId = 1;

    public FactionManager(PersistentEmpiresPlugin plugin) {
        this.plugin = plugin;
        loadFactions();
    }

    private void loadFactions() {
        List<PEFaction> loadedFactions = plugin.getDatabaseManager().loadAllFactions();
        for (PEFaction faction : loadedFactions) {
            factions.put(faction.getId(), faction);
            if (faction.getId() >= nextFactionId) {
                nextFactionId = faction.getId() + 1;
            }
        }
        plugin.getLogger().info("Loaded " + factions.size() + " factions");
    }

    public PEFaction createFaction(String name, String displayName, String lordUUID) {
        // Check if faction name already exists
        if (getFactionByName(name) != null) {
            return null;
        }

        // Check if lord is already in a faction
        if (isPlayerInFaction(lordUUID)) {
            return null;
        }

        // Create new faction
        PEFaction faction = new PEFaction(nextFactionId++, name, displayName, lordUUID);
        factions.put(faction.getId(), faction);
        playerFactions.put(lordUUID, faction.getId());
        
        // Save to database
        plugin.getDatabaseManager().saveFaction(faction);
        
        plugin.getLogger().info("Created faction: " + name + " (ID: " + faction.getId() + ") with lord: " + lordUUID);
        return faction;
    }

    public boolean joinFaction(String playerUUID, int factionId) {
        PEFaction faction = factions.get(factionId);
        if (faction == null) {
            return false;
        }

        // Check if player is already in a faction
        if (isPlayerInFaction(playerUUID)) {
            return false;
        }

        playerFactions.put(playerUUID, factionId);
        
        // Update player's faction in database
        PEPlayer player = plugin.getPlayerManager().getPlayerData(playerUUID);
        if (player != null) {
            player.setFactionId(factionId);
            plugin.getDatabaseManager().savePlayer(player);
        }

        plugin.getLogger().info("Player " + playerUUID + " joined faction " + faction.getName());
        return true;
    }

    public boolean leaveFaction(String playerUUID) {
        Integer factionId = playerFactions.get(playerUUID);
        if (factionId == null) {
            return false;
        }

        PEFaction faction = factions.get(factionId);
        if (faction == null) {
            return false;
        }

        // Check if player is the lord
        if (faction.isLord(playerUUID)) {
            // Transfer lordship or disband faction
            if (faction.getMarshalls().isEmpty()) {
                // Disband faction if no marshalls
                disbandFaction(factionId);
            } else {
                // Transfer lordship to first marshall
                String newLord = faction.getMarshalls().get(0);
                faction.setLordUUID(newLord);
                faction.getMarshalls().remove(newLord);
                plugin.getDatabaseManager().saveFaction(faction);
            }
        } else {
            // Remove from management positions
            faction.removeMarshall(playerUUID);
            faction.removeDoorManager(playerUUID);
            faction.removeChestManager(playerUUID);
            plugin.getDatabaseManager().saveFaction(faction);
        }

        playerFactions.remove(playerUUID);
        
        // Update player's faction in database
        PEPlayer player = plugin.getPlayerManager().getPlayerData(playerUUID);
        if (player != null) {
            player.setFactionId(0);
            plugin.getDatabaseManager().savePlayer(player);
        }

        plugin.getLogger().info("Player " + playerUUID + " left faction " + faction.getName());
        return true;
    }

    public boolean disbandFaction(int factionId) {
        PEFaction faction = factions.remove(factionId);
        if (faction == null) {
            return false;
        }

        // Remove all players from the faction
        playerFactions.entrySet().removeIf(entry -> entry.getValue().equals(factionId));
        
        // Update all players' faction in database
        // This would be done through a database query in a real implementation
        
        plugin.getLogger().info("Disbanded faction: " + faction.getName() + " (ID: " + factionId + ")");
        return true;
    }

    public boolean declareWar(int attackerFactionId, int defenderFactionId) {
        PEFaction attacker = factions.get(attackerFactionId);
        PEFaction defender = factions.get(defenderFactionId);
        
        if (attacker == null || defender == null) {
            return false;
        }

        // Check cooldowns
        long currentTime = System.currentTimeMillis();
        Long lastWarTime = warCooldowns.get(attackerFactionId);
        if (lastWarTime != null) {
            long warTimeout = plugin.getConfig().getInt("factions.war_declare_timeout", 30) * 60 * 1000L;
            if (currentTime - lastWarTime < warTimeout) {
                return false;
            }
        }

        // Declare war
        attacker.declareWar(defenderFactionId);
        defender.declareWar(attackerFactionId);
        
        // Update database
        plugin.getDatabaseManager().saveFaction(attacker);
        plugin.getDatabaseManager().saveFaction(defender);
        
        // Set cooldown
        warCooldowns.put(attackerFactionId, currentTime);
        
        plugin.getLogger().info("War declared between " + attacker.getName() + " and " + defender.getName());
        return true;
    }

    public boolean makePeace(int factionId1, int factionId2) {
        PEFaction faction1 = factions.get(factionId1);
        PEFaction faction2 = factions.get(factionId2);
        
        if (faction1 == null || faction2 == null) {
            return false;
        }

        // Check cooldowns
        long currentTime = System.currentTimeMillis();
        Long lastPeaceTime = peaceCooldowns.get(factionId1);
        if (lastPeaceTime != null) {
            long peaceTimeout = plugin.getConfig().getInt("factions.peace_declare_timeout", 30) * 60 * 1000L;
            if (currentTime - lastPeaceTime < peaceTimeout) {
                return false;
            }
        }

        // Make peace
        faction1.makePeace(factionId2);
        faction2.makePeace(factionId1);
        
        // Update database
        plugin.getDatabaseManager().saveFaction(faction1);
        plugin.getDatabaseManager().saveFaction(faction2);
        
        // Set cooldown
        peaceCooldowns.put(factionId1, currentTime);
        
        plugin.getLogger().info("Peace made between " + faction1.getName() + " and " + faction2.getName());
        return true;
    }

    public boolean addMarshall(int factionId, String playerUUID) {
        PEFaction faction = factions.get(factionId);
        if (faction == null) {
            return false;
        }

        faction.addMarshall(playerUUID);
        plugin.getDatabaseManager().saveFaction(faction);
        
        plugin.getLogger().info("Added marshall " + playerUUID + " to faction " + faction.getName());
        return true;
    }

    public boolean removeMarshall(int factionId, String playerUUID) {
        PEFaction faction = factions.get(factionId);
        if (faction == null) {
            return false;
        }

        faction.removeMarshall(playerUUID);
        plugin.getDatabaseManager().saveFaction(faction);
        
        plugin.getLogger().info("Removed marshall " + playerUUID + " from faction " + faction.getName());
        return true;
    }

    public void processWarCooldowns() {
        long currentTime = System.currentTimeMillis();
        long warTimeout = plugin.getConfig().getInt("factions.war_declare_timeout", 30) * 60 * 1000L;
        long peaceTimeout = plugin.getConfig().getInt("factions.peace_declare_timeout", 30) * 60 * 1000L;
        
        warCooldowns.entrySet().removeIf(entry -> currentTime - entry.getValue() > warTimeout);
        peaceCooldowns.entrySet().removeIf(entry -> currentTime - entry.getValue() > peaceTimeout);
    }

    // Getters and utility methods
    public PEFaction getFaction(int factionId) {
        return factions.get(factionId);
    }

    public PEFaction getFactionByName(String name) {
        return factions.values().stream()
                .filter(f -> f.getName().equalsIgnoreCase(name))
                .findFirst()
                .orElse(null);
    }

    public PEFaction getPlayerFaction(String playerUUID) {
        Integer factionId = playerFactions.get(playerUUID);
        return factionId != null ? factions.get(factionId) : null;
    }

    public boolean isPlayerInFaction(String playerUUID) {
        return playerFactions.containsKey(playerUUID);
    }

    public boolean areAtWar(int factionId1, int factionId2) {
        PEFaction faction1 = factions.get(factionId1);
        return faction1 != null && faction1.isAtWarWith(factionId2);
    }

    public Map<Integer, PEFaction> getAllFactions() {
        return new HashMap<>(factions);
    }

    public int getPlayerFactionId(String playerUUID) {
        return playerFactions.getOrDefault(playerUUID, 0);
    }
}