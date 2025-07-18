package com.persistentempires.minecraft.managers;

import com.persistentempires.minecraft.PersistentEmpiresPlugin;
import com.persistentempires.minecraft.database.entities.PEPlayer;
import org.bukkit.entity.Player;
import org.bukkit.attribute.Attribute;
import org.bukkit.attribute.AttributeInstance;
import org.bukkit.inventory.ItemStack;
import org.bukkit.inventory.meta.ItemMeta;

import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;

/**
 * Manages combat mechanics including hunger, healing, armor weight, and combat logging
 */
public class CombatManager {
    private final PersistentEmpiresPlugin plugin;
    private final Map<String, Long> lastDamageTime = new ConcurrentHashMap<>();
    private final Map<String, Float> armorWeightCache = new ConcurrentHashMap<>();

    public CombatManager(PersistentEmpiresPlugin plugin) {
        this.plugin = plugin;
    }

    /**
     * Processes hunger and health regeneration for all online players
     */
    public void processHungerAndHealth() {
        for (Player player : plugin.getServer().getOnlinePlayers()) {
            processPlayerHungerAndHealth(player);
        }
    }

    private void processPlayerHungerAndHealth(Player player) {
        PEPlayer data = plugin.getPlayerManager().getPlayerData(player);
        if (data == null) return;

        // Process hunger reduction
        long currentTime = System.currentTimeMillis();
        int hungerInterval = plugin.getConfig().getInt("hunger.hunger_interval", 72) * 1000;
        
        // Reduce hunger over time
        if (currentTime % hungerInterval < 1000) { // Approximately every hunger interval
            int hungerReduce = plugin.getConfig().getInt("hunger.hunger_reduce_amount", 1);
            data.reduceHunger(hungerReduce);
            player.setFoodLevel(Math.max(0, Math.min(20, data.getHunger())));
        }

        // Process health regeneration based on hunger
        int hungerBoundary = plugin.getConfig().getInt("hunger.hunger_heal_boundary", 5);
        int startHealingThreshold = plugin.getConfig().getInt("hunger.start_healing_threshold", 75);
        
        if (data.getHunger() > hungerBoundary && player.getHealth() < (startHealingThreshold / 100.0 * 20.0)) {
            int healingAmount = plugin.getConfig().getInt("hunger.hunger_healing_amount", 2);
            int healingCost = plugin.getConfig().getInt("hunger.hunger_healing_cost", 5);
            
            if (data.getHunger() >= healingCost) {
                player.setHealth(Math.min(20, player.getHealth() + healingAmount));
                data.reduceHunger(healingCost);
                player.setFoodLevel(Math.max(0, Math.min(20, data.getHunger())));
            }
        }

        // Process starvation damage
        if (data.getHunger() <= 0) {
            player.damage(1.0); // Starvation damage
        }
    }

    /**
     * Handles player taking damage and applies combat log
     */
    public void handlePlayerDamage(Player player, double damage) {
        String uuid = player.getUniqueId().toString();
        long currentTime = System.currentTimeMillis();
        
        // Update last damage time
        lastDamageTime.put(uuid, currentTime);
        
        // Apply combat log
        int combatLogDuration = plugin.getConfig().getInt("combat.combat_log_duration", 15);
        plugin.getPlayerManager().setCombatLog(player, combatLogDuration);
        
        // Update armor weight and movement speed
        updateMovementSpeed(player);
        
        // Check for wounding
        if (player.getHealth() <= 4.0) { // Low health
            plugin.getPlayerManager().setWounded(player, 30); // 30 seconds wounded
        }
    }

    /**
     * Handles player death and applies death penalties
     */
    public void handlePlayerDeath(Player player) {
        PEPlayer data = plugin.getPlayerManager().getPlayerData(player);
        if (data == null) return;

        // Drop money on death
        int dropPercentage = plugin.getConfig().getInt("combat.death_money_drop_percentage", 25);
        int goldToDrop = (int) (data.getGold() * (dropPercentage / 100.0));
        
        if (goldToDrop > 0) {
            data.removeGold(goldToDrop);
            // Drop gold as items in the world
            dropGoldItems(player, goldToDrop);
        }

        // Apply death penalty
        plugin.getPlayerManager().setWounded(player, 60); // 60 seconds wounded
        
        // Clear combat log
        data.setCombatLogUntil(0);
        
        // Reset hunger and health
        data.setHunger(50); // Half hunger on death
        data.setHealth(20); // Full health on respawn
    }

    /**
     * Calculates and applies armor weight effects on movement speed
     */
    public void updateMovementSpeed(Player player) {
        if (!plugin.getConfig().getBoolean("armor.weight_affects_movement", true)) {
            return;
        }

        String uuid = player.getUniqueId().toString();
        float armorWeight = calculateArmorWeight(player);
        armorWeightCache.put(uuid, armorWeight);

        // Calculate speed reduction
        float baseSpeed = (float) plugin.getConfig().getDouble("armor.base_speed_multiplier", 1.0);
        float speedReduction = (float) plugin.getConfig().getDouble("armor.speed_reduction_per_weight", 0.05);
        float maxReduction = (float) plugin.getConfig().getDouble("armor.max_speed_reduction", 0.5);
        
        float speedMultiplier = Math.max(baseSpeed - maxReduction, baseSpeed - (armorWeight * speedReduction));
        
        // Apply speed to player
        AttributeInstance speedAttribute = player.getAttribute(Attribute.GENERIC_MOVEMENT_SPEED);
        if (speedAttribute != null) {
            speedAttribute.setBaseValue(0.1 * speedMultiplier); // 0.1 is default walking speed
        }
    }

    /**
     * Calculates the total weight of armor worn by the player
     */
    private float calculateArmorWeight(Player player) {
        float totalWeight = 0.0f;
        
        // Check helmet
        ItemStack helmet = player.getInventory().getHelmet();
        if (helmet != null) {
            totalWeight += getItemWeight(helmet);
        }
        
        // Check chestplate
        ItemStack chestplate = player.getInventory().getChestplate();
        if (chestplate != null) {
            totalWeight += getItemWeight(chestplate);
        }
        
        // Check leggings
        ItemStack leggings = player.getInventory().getLeggings();
        if (leggings != null) {
            totalWeight += getItemWeight(leggings);
        }
        
        // Check boots
        ItemStack boots = player.getInventory().getBoots();
        if (boots != null) {
            totalWeight += getItemWeight(boots);
        }
        
        return totalWeight;
    }

    /**
     * Gets the weight of an item based on its material and enchantments
     */
    private float getItemWeight(ItemStack item) {
        if (item == null) return 0.0f;
        
        String material = item.getType().toString();
        float weight = 0.0f;
        
        // Assign weights based on material
        if (material.contains("LEATHER")) {
            weight = 1.0f;
        } else if (material.contains("CHAINMAIL")) {
            weight = 2.0f;
        } else if (material.contains("IRON")) {
            weight = 3.0f;
        } else if (material.contains("DIAMOND")) {
            weight = 4.0f;
        } else if (material.contains("NETHERITE")) {
            weight = 5.0f;
        }
        
        // Increase weight based on enchantments
        if (item.hasItemMeta()) {
            ItemMeta meta = item.getItemMeta();
            if (meta != null && meta.hasEnchants()) {
                weight += meta.getEnchants().size() * 0.5f;
            }
        }
        
        return weight;
    }

    /**
     * Heals a player using medicine items
     */
    public boolean healPlayer(Player healer, Player target) {
        int requiredLevel = plugin.getConfig().getInt("combat.required_medicine_level", 50);
        int healingAmount = plugin.getConfig().getInt("combat.medicine_healing_amount", 4);
        
        // Check if healer has required medicine skill level
        // This would be implemented with a skill system
        
        // Check if target can be healed
        if (target.getHealth() >= 20.0) {
            return false;
        }
        
        // Apply healing
        target.setHealth(Math.min(20.0, target.getHealth() + healingAmount));
        
        // Remove medicine item from healer's inventory
        // This would remove the medicine item
        
        return true;
    }

    /**
     * Applies poison effect to a player
     */
    public void poisonPlayer(Player player, int duration) {
        // Apply poison effect
        player.addPotionEffect(new org.bukkit.potion.PotionEffect(
            org.bukkit.potion.PotionEffectType.POISON, duration * 20, 0));
    }

    /**
     * Cures poison from a player
     */
    public void curePoison(Player player) {
        player.removePotionEffect(org.bukkit.potion.PotionEffectType.POISON);
    }

    /**
     * Drops gold items in the world
     */
    private void dropGoldItems(Player player, int goldAmount) {
        org.bukkit.Location location = player.getLocation();
        
        // Drop gold as gold nuggets (1 gold = 1 nugget)
        int stacks = goldAmount / 64;
        int remainder = goldAmount % 64;
        
        for (int i = 0; i < stacks; i++) {
            ItemStack goldStack = new ItemStack(org.bukkit.Material.GOLD_NUGGET, 64);
            location.getWorld().dropItem(location, goldStack);
        }
        
        if (remainder > 0) {
            ItemStack goldStack = new ItemStack(org.bukkit.Material.GOLD_NUGGET, remainder);
            location.getWorld().dropItem(location, goldStack);
        }
    }

    // Getters and utility methods
    public boolean isInCombat(Player player) {
        return plugin.getPlayerManager().isInCombat(player);
    }

    public boolean isWounded(Player player) {
        return plugin.getPlayerManager().isWounded(player);
    }

    public long getLastDamageTime(String uuid) {
        return lastDamageTime.getOrDefault(uuid, 0L);
    }

    public float getArmorWeight(String uuid) {
        return armorWeightCache.getOrDefault(uuid, 0.0f);
    }
}