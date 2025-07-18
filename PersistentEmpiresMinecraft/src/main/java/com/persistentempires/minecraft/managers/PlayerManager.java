package com.persistentempires.minecraft.managers;

import com.persistentempires.minecraft.PersistentEmpiresPlugin;
import com.persistentempires.minecraft.database.entities.PEPlayer;
import org.bukkit.entity.Player;

import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;

/**
 * Manages player data, including persistence, stats, and session management
 */
public class PlayerManager {
    private final PersistentEmpiresPlugin plugin;
    private final Map<String, PEPlayer> playerData = new ConcurrentHashMap<>();
    private final Map<String, Long> lastSave = new ConcurrentHashMap<>();

    public PlayerManager(PersistentEmpiresPlugin plugin) {
        this.plugin = plugin;
    }

    public void loadPlayer(Player player) {
        String uuid = player.getUniqueId().toString();
        
        // Try to load from database
        PEPlayer data = plugin.getDatabaseManager().loadPlayer(uuid);
        
        if (data == null) {
            // Create new player data
            data = new PEPlayer(uuid, player.getName());
            plugin.getLogger().info("Created new player data for: " + player.getName());
        } else {
            // Update name if changed
            data.setPlayerName(player.getName());
            plugin.getLogger().info("Loaded existing player data for: " + player.getName());
        }
        
        data.setLastLogin(System.currentTimeMillis());
        playerData.put(uuid, data);
        
        // Apply player data to Minecraft player
        applyPlayerData(player, data);
    }

    public void savePlayer(Player player) {
        String uuid = player.getUniqueId().toString();
        PEPlayer data = playerData.get(uuid);
        
        if (data != null) {
            // Update data from Minecraft player
            updatePlayerData(player, data);
            
            // Save to database
            plugin.getDatabaseManager().savePlayer(data);
            lastSave.put(uuid, System.currentTimeMillis());
            
            plugin.getLogger().info("Saved player data for: " + player.getName());
        }
    }

    public void unloadPlayer(Player player) {
        String uuid = player.getUniqueId().toString();
        
        // Save before unloading
        savePlayer(player);
        
        // Remove from memory
        playerData.remove(uuid);
        lastSave.remove(uuid);
        
        plugin.getLogger().info("Unloaded player data for: " + player.getName());
    }

    public void saveAllPlayers() {
        int saved = 0;
        for (Player player : plugin.getServer().getOnlinePlayers()) {
            savePlayer(player);
            saved++;
        }
        plugin.getLogger().info("Auto-saved " + saved + " players");
    }

    private void applyPlayerData(Player player, PEPlayer data) {
        // Apply health (convert from PE scale to Minecraft scale)
        player.setHealth(Math.max(1, Math.min(20, data.getHealth())));
        
        // Apply hunger (convert from PE scale to Minecraft scale)
        player.setFoodLevel(Math.max(0, Math.min(20, data.getHunger())));
        
        // Apply experience
        player.setTotalExperience(data.getExperience());
        
        // Apply location if same world
        if (player.getWorld().getName().equals(data.getWorld())) {
            player.teleport(new org.bukkit.Location(player.getWorld(), data.getPosX(), data.getPosY(), data.getPosZ()));
        }
        
        // Apply custom display name if set
        if (!data.getCustomName().isEmpty()) {
            player.setDisplayName(data.getCustomName());
        }
    }

    private void updatePlayerData(Player player, PEPlayer data) {
        // Update health
        data.setHealth((int) player.getHealth());
        
        // Update hunger
        data.setHunger(player.getFoodLevel());
        
        // Update experience
        data.setExperience(player.getTotalExperience());
        
        // Update location
        org.bukkit.Location loc = player.getLocation();
        data.setPosX(loc.getX());
        data.setPosY(loc.getY());
        data.setPosZ(loc.getZ());
        data.setWorld(loc.getWorld().getName());
        
        // Update custom name
        if (player.getDisplayName() != null && !player.getDisplayName().equals(player.getName())) {
            data.setCustomName(player.getDisplayName());
        }
    }

    public PEPlayer getPlayerData(String uuid) {
        return playerData.get(uuid);
    }

    public PEPlayer getPlayerData(Player player) {
        return getPlayerData(player.getUniqueId().toString());
    }

    public boolean hasPlayerData(String uuid) {
        return playerData.containsKey(uuid);
    }

    public void setCombatLog(Player player, int durationSeconds) {
        PEPlayer data = getPlayerData(player);
        if (data != null) {
            data.setCombatLog(durationSeconds);
        }
    }

    public boolean isInCombat(Player player) {
        PEPlayer data = getPlayerData(player);
        return data != null && data.isInCombat();
    }

    public void setWounded(Player player, int durationSeconds) {
        PEPlayer data = getPlayerData(player);
        if (data != null) {
            data.setWounded(durationSeconds);
        }
    }

    public boolean isWounded(Player player) {
        PEPlayer data = getPlayerData(player);
        return data != null && data.isWounded();
    }

    public boolean addGold(Player player, int amount) {
        PEPlayer data = getPlayerData(player);
        if (data != null) {
            data.addGold(amount);
            return true;
        }
        return false;
    }

    public boolean removeGold(Player player, int amount) {
        PEPlayer data = getPlayerData(player);
        if (data != null) {
            return data.removeGold(amount);
        }
        return false;
    }

    public int getGold(Player player) {
        PEPlayer data = getPlayerData(player);
        return data != null ? data.getGold() : 0;
    }

    public boolean addBankAmount(Player player, int amount) {
        PEPlayer data = getPlayerData(player);
        if (data != null) {
            int bankLimit = plugin.getConfig().getInt("economy.bank_limit", 1000000);
            if (data.getBankAmount() + amount <= bankLimit) {
                data.addBankAmount(amount);
                return true;
            }
        }
        return false;
    }

    public boolean removeBankAmount(Player player, int amount) {
        PEPlayer data = getPlayerData(player);
        if (data != null) {
            return data.removeBankAmount(amount);
        }
        return false;
    }

    public int getBankAmount(Player player) {
        PEPlayer data = getPlayerData(player);
        return data != null ? data.getBankAmount() : 0;
    }

    public void addExperience(Player player, int amount) {
        PEPlayer data = getPlayerData(player);
        if (data != null) {
            data.addExperience(amount);
        }
    }

    public void reduceHunger(Player player, int amount) {
        PEPlayer data = getPlayerData(player);
        if (data != null) {
            data.reduceHunger(amount);
            player.setFoodLevel(Math.max(0, Math.min(20, data.getHunger())));
        }
    }

    public void restoreHunger(Player player, int amount) {
        PEPlayer data = getPlayerData(player);
        if (data != null) {
            data.restoreHunger(amount);
            player.setFoodLevel(Math.max(0, Math.min(20, data.getHunger())));
        }
    }

    public void setCustomName(Player player, String customName) {
        PEPlayer data = getPlayerData(player);
        if (data != null) {
            data.setCustomName(customName);
            player.setDisplayName(customName);
        }
    }

    public String getCustomName(Player player) {
        PEPlayer data = getPlayerData(player);
        return data != null ? data.getCustomName() : "";
    }

    public void setPlayerClass(Player player, String playerClass) {
        PEPlayer data = getPlayerData(player);
        if (data != null) {
            data.setPlayerClass(playerClass);
        }
    }

    public String getPlayerClass(Player player) {
        PEPlayer data = getPlayerData(player);
        return data != null ? data.getPlayerClass() : "peasant";
    }
}