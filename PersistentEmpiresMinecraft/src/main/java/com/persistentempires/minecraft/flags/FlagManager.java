package com.persistentempires.minecraft.flags;

import com.persistentempires.minecraft.PersistentEmpiresPlugin;
import com.persistentempires.minecraft.database.entities.PEFlag;
import com.persistentempires.minecraft.database.entities.PEFaction;
import org.bukkit.Location;
import org.bukkit.Material;
import org.bukkit.block.Block;
import org.bukkit.block.BlockState;
import org.bukkit.block.banner.Pattern;
import org.bukkit.block.banner.PatternType;
import org.bukkit.DyeColor;
import org.bukkit.entity.Player;
import org.bukkit.inventory.ItemStack;

import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;

/**
 * Manages flag capturing system - flags that can be captured to claim territory
 */
public class FlagManager {
    private final PersistentEmpiresPlugin plugin;
    private final Map<Integer, PEFlag> flags = new ConcurrentHashMap<>();
    private final Map<String, FlagCapture> activeCaptures = new ConcurrentHashMap<>();
    private final Map<String, Long> captureCooldowns = new ConcurrentHashMap<>();
    private int nextFlagId = 1;

    public FlagManager(PersistentEmpiresPlugin plugin) {
        this.plugin = plugin;
        loadFlags();
        startCaptureProcessor();
    }

    private void loadFlags() {
        // Load flags from database
        // This would query the database for all flags
        plugin.getLogger().info("Loaded " + flags.size() + " flags");
    }

    public PEFlag createFlag(String name, Location location) {
        if (getFlagByName(name) != null) {
            return null;
        }

        PEFlag flag = new PEFlag(nextFlagId++, name, location.getWorld().getName(),
                location.getBlockX(), location.getBlockY(), location.getBlockZ());
        flags.put(flag.getId(), flag);
        
        // Place banner block at location
        placeFlagBanner(location, flag);
        
        // Save to database
        saveFlagToDatabase(flag);
        
        plugin.getLogger().info("Created flag: " + name + " at " + location);
        return flag;
    }

    public boolean startCapture(Player player, PEFlag flag) {
        String playerUUID = player.getUniqueId().toString();
        
        // Check if player is already capturing
        if (activeCaptures.containsKey(playerUUID)) {
            return false;
        }

        // Check capture cooldown
        Long lastCapture = captureCooldowns.get(playerUUID);
        if (lastCapture != null) {
            long cooldown = plugin.getConfig().getInt("flags.capture_cooldown", 60) * 1000L;
            if (System.currentTimeMillis() - lastCapture < cooldown) {
                return false;
            }
        }

        // Check if player has required item
        String requiredItem = plugin.getConfig().getString("flags.capture_item", "BANNER");
        if (!player.getInventory().contains(Material.valueOf(requiredItem))) {
            return false;
        }

        // Check if player is in range
        Location playerLoc = player.getLocation();
        if (!flag.isInRange(playerLoc.getX(), playerLoc.getY(), playerLoc.getZ())) {
            return false;
        }

        // Check if player's faction can capture this flag
        PEFaction playerFaction = plugin.getFactionManager().getPlayerFaction(playerUUID);
        if (playerFaction == null) {
            return false;
        }

        // Check if flag is already owned by player's faction
        if (flag.isOwnedBy(playerFaction.getId())) {
            return false;
        }

        // Check if player's faction is at war with the flag owner (if any)
        if (flag.getFactionId() > 0) {
            PEFaction flagOwner = plugin.getFactionManager().getFaction(flag.getFactionId());
            if (flagOwner != null && !plugin.getFactionManager().areAtWar(playerFaction.getId(), flagOwner.getId())) {
                return false;
            }
        }

        // Start capture
        FlagCapture capture = new FlagCapture(flag, player, System.currentTimeMillis());
        activeCaptures.put(playerUUID, capture);
        
        // Send message to player
        player.sendMessage(plugin.getConfigManager().getMessage("capture_started")
                .replace("{flag}", flag.getName())
                .replace("{duration}", String.valueOf(flag.getCaptureDuration())));
        
        plugin.getLogger().info("Player " + player.getName() + " started capturing flag " + flag.getName());
        return true;
    }

    public void cancelCapture(Player player) {
        String playerUUID = player.getUniqueId().toString();
        FlagCapture capture = activeCaptures.remove(playerUUID);
        if (capture != null) {
            player.sendMessage(plugin.getConfigManager().getMessage("capture_cancelled"));
            plugin.getLogger().info("Player " + player.getName() + " cancelled flag capture");
        }
    }

    private void startCaptureProcessor() {
        plugin.getServer().getScheduler().runTaskTimer(plugin, () -> {
            List<String> completedCaptures = new ArrayList<>();
            
            for (Map.Entry<String, FlagCapture> entry : activeCaptures.entrySet()) {
                String playerUUID = entry.getKey();
                FlagCapture capture = entry.getValue();
                
                Player player = plugin.getServer().getPlayer(capture.getPlayer().getUniqueId());
                if (player == null || !player.isOnline()) {
                    completedCaptures.add(playerUUID);
                    continue;
                }

                // Check if player is still in range
                Location playerLoc = player.getLocation();
                if (!capture.getFlag().isInRange(playerLoc.getX(), playerLoc.getY(), playerLoc.getZ())) {
                    completedCaptures.add(playerUUID);
                    player.sendMessage(plugin.getConfigManager().getMessage("capture_failed_range"));
                    continue;
                }

                // Check if capture is complete
                long elapsed = System.currentTimeMillis() - capture.getStartTime();
                if (elapsed >= capture.getFlag().getCaptureDuration() * 1000L) {
                    // Complete capture
                    completeCapture(capture);
                    completedCaptures.add(playerUUID);
                } else {
                    // Send progress update
                    int progress = (int) ((elapsed / 1000.0) / capture.getFlag().getCaptureDuration() * 100);
                    player.sendMessage("§6Capturing " + capture.getFlag().getName() + "... " + progress + "%");
                }
            }
            
            // Remove completed captures
            completedCaptures.forEach(activeCaptures::remove);
        }, 20L, 20L); // Run every second
    }

    private void completeCapture(FlagCapture capture) {
        Player player = capture.getPlayer();
        PEFlag flag = capture.getFlag();
        String playerUUID = player.getUniqueId().toString();
        
        // Get player's faction
        PEFaction playerFaction = plugin.getFactionManager().getPlayerFaction(playerUUID);
        if (playerFaction == null) {
            player.sendMessage(plugin.getConfigManager().getMessage("capture_failed_no_faction"));
            return;
        }

        // Capture the flag
        flag.capture(playerFaction.getId(), playerUUID);
        
        // Update flag banner
        updateFlagBanner(flag, playerFaction);
        
        // Save to database
        saveFlagToDatabase(flag);
        
        // Set cooldown
        captureCooldowns.put(playerUUID, System.currentTimeMillis());
        
        // Send messages
        player.sendMessage(plugin.getConfigManager().getMessage("flag_captured")
                .replace("{flag}", flag.getName())
                .replace("{faction}", playerFaction.getDisplayName()));
        
        // Broadcast to faction members
        broadcastToFaction(playerFaction, 
                plugin.getConfigManager().getMessage("faction_captured_flag")
                        .replace("{player}", player.getName())
                        .replace("{flag}", flag.getName()));
        
        plugin.getLogger().info("Flag " + flag.getName() + " captured by " + player.getName() + 
                " for faction " + playerFaction.getName());
    }

    private void placeFlagBanner(Location location, PEFlag flag) {
        Block block = location.getBlock();
        block.setType(Material.WHITE_BANNER);
        // Additional banner customization can be added here
    }

    private void updateFlagBanner(PEFlag flag, PEFaction faction) {
        Location location = new Location(
                plugin.getServer().getWorld(flag.getWorld()),
                flag.getX(), flag.getY(), flag.getZ()
        );
        
        Block block = location.getBlock();
        if (block.getType().toString().contains("BANNER")) {
            BlockState state = block.getState();
            if (state instanceof org.bukkit.block.Banner) {
                org.bukkit.block.Banner banner = (org.bukkit.block.Banner) state;
                
                // Set faction colors/patterns
                banner.setBaseColor(DyeColor.WHITE);
                List<Pattern> patterns = new ArrayList<>();
                patterns.add(new Pattern(DyeColor.RED, PatternType.STRIPE_TOP));
                patterns.add(new Pattern(DyeColor.BLUE, PatternType.STRIPE_BOTTOM));
                banner.setPatterns(patterns);
                
                banner.update();
            }
        }
    }

    private void broadcastToFaction(PEFaction faction, String message) {
        for (Player player : plugin.getServer().getOnlinePlayers()) {
            PEFaction playerFaction = plugin.getFactionManager().getPlayerFaction(player.getUniqueId().toString());
            if (playerFaction != null && playerFaction.getId() == faction.getId()) {
                player.sendMessage(message);
            }
        }
    }

    private void saveFlagToDatabase(PEFlag flag) {
        // Save flag to database
        // This would be implemented with proper database queries
    }

    // Getters and utility methods
    public PEFlag getFlag(int flagId) {
        return flags.get(flagId);
    }

    public PEFlag getFlagByName(String name) {
        return flags.values().stream()
                .filter(f -> f.getName().equalsIgnoreCase(name))
                .findFirst()
                .orElse(null);
    }

    public PEFlag getFlagAt(Location location) {
        return flags.values().stream()
                .filter(f -> f.getWorld().equals(location.getWorld().getName()) &&
                        f.getX() == location.getBlockX() &&
                        f.getY() == location.getBlockY() &&
                        f.getZ() == location.getBlockZ())
                .findFirst()
                .orElse(null);
    }

    public boolean isPlayerCapturing(Player player) {
        return activeCaptures.containsKey(player.getUniqueId().toString());
    }

    public List<PEFlag> getFlagsOwnedBy(int factionId) {
        return flags.values().stream()
                .filter(f -> f.getFactionId() == factionId)
                .toList();
    }

    public Map<Integer, PEFlag> getAllFlags() {
        return new ConcurrentHashMap<>(flags);
    }

    // Inner class for tracking active captures
    private static class FlagCapture {
        private final PEFlag flag;
        private final Player player;
        private final long startTime;

        public FlagCapture(PEFlag flag, Player player, long startTime) {
            this.flag = flag;
            this.player = player;
            this.startTime = startTime;
        }

        public PEFlag getFlag() { return flag; }
        public Player getPlayer() { return player; }
        public long getStartTime() { return startTime; }
    }
}