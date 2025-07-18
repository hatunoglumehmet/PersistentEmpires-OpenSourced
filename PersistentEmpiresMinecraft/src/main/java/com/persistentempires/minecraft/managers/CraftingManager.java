package com.persistentempires.minecraft.managers;

import com.persistentempires.minecraft.PersistentEmpiresPlugin;
import org.bukkit.Material;
import org.bukkit.entity.Player;
import org.bukkit.inventory.ItemStack;
import org.bukkit.inventory.meta.ItemMeta;

import java.util.*;
import java.util.concurrent.ConcurrentHashMap;

/**
 * Manages crafting recipes and crafting operations
 */
public class CraftingManager {
    private final PersistentEmpiresPlugin plugin;
    private final Map<String, CraftingRecipe> recipes = new ConcurrentHashMap<>();
    private final Map<String, CraftingSession> activeCrafting = new ConcurrentHashMap<>();

    public CraftingManager(PersistentEmpiresPlugin plugin) {
        this.plugin = plugin;
        loadDefaultRecipes();
    }

    private void loadDefaultRecipes() {
        // Load default Persistent Empires style recipes
        
        // Basic tools
        addRecipe(new CraftingRecipe("iron_sword", "Iron Sword")
                .addIngredient(Material.IRON_INGOT, 2)
                .addIngredient(Material.STICK, 1)
                .setResult(Material.IRON_SWORD, 1)
                .setCraftingTime(30)
                .setRequiredLevel(25)
                .setSuccessRate(85));

        addRecipe(new CraftingRecipe("chainmail_helmet", "Chainmail Helmet")
                .addIngredient(Material.IRON_NUGGET, 16)
                .addIngredient(Material.STRING, 4)
                .setResult(Material.CHAINMAIL_HELMET, 1)
                .setCraftingTime(45)
                .setRequiredLevel(30)
                .setSuccessRate(80));

        // Advanced weapons
        addRecipe(new CraftingRecipe("diamond_sword", "Diamond Sword")
                .addIngredient(Material.DIAMOND, 2)
                .addIngredient(Material.STICK, 1)
                .addIngredient(Material.LEATHER, 1)
                .setResult(Material.DIAMOND_SWORD, 1)
                .setCraftingTime(60)
                .setRequiredLevel(50)
                .setSuccessRate(75));

        // Armor sets
        addRecipe(new CraftingRecipe("iron_chestplate", "Iron Chestplate")
                .addIngredient(Material.IRON_INGOT, 8)
                .addIngredient(Material.LEATHER, 2)
                .setResult(Material.IRON_CHESTPLATE, 1)
                .setCraftingTime(90)
                .setRequiredLevel(35)
                .setSuccessRate(80));

        // Medical items
        addRecipe(new CraftingRecipe("health_potion", "Health Potion")
                .addIngredient(Material.GLASS_BOTTLE, 1)
                .addIngredient(Material.GOLDEN_APPLE, 1)
                .addIngredient(Material.SPIDER_EYE, 1)
                .setResult(Material.POTION, 1)
                .setCraftingTime(20)
                .setRequiredLevel(20)
                .setSuccessRate(90));

        // Siege equipment parts
        addRecipe(new CraftingRecipe("siege_ladder", "Siege Ladder")
                .addIngredient(Material.OAK_PLANKS, 12)
                .addIngredient(Material.STICK, 8)
                .addIngredient(Material.IRON_NUGGET, 4)
                .setResult(Material.LADDER, 8)
                .setCraftingTime(120)
                .setRequiredLevel(40)
                .setSuccessRate(85));

        // Food items
        addRecipe(new CraftingRecipe("bread_loaf", "Bread Loaf")
                .addIngredient(Material.WHEAT, 3)
                .addIngredient(Material.WATER_BUCKET, 1)
                .setResult(Material.BREAD, 4)
                .setCraftingTime(15)
                .setRequiredLevel(5)
                .setSuccessRate(95));

        plugin.getLogger().info("Loaded " + recipes.size() + " default crafting recipes");
    }

    /**
     * Starts a crafting session for a player
     */
    public boolean startCrafting(Player player, String recipeName) {
        CraftingRecipe recipe = recipes.get(recipeName);
        if (recipe == null) {
            player.sendMessage("§cRecipe not found: " + recipeName);
            return false;
        }

        // Check if player is already crafting
        if (activeCrafting.containsKey(player.getUniqueId().toString())) {
            player.sendMessage("§cYou are already crafting something!");
            return false;
        }

        // Check if player has required level
        int playerLevel = plugin.getPlayerManager().getPlayerData(player).getExperience() / 100; // Convert XP to level
        if (playerLevel < recipe.getRequiredLevel()) {
            player.sendMessage("§cYou need level " + recipe.getRequiredLevel() + " to craft this item!");
            return false;
        }

        // Check if player has required ingredients
        if (!hasRequiredIngredients(player, recipe)) {
            player.sendMessage("§cYou don't have the required ingredients!");
            showRequiredIngredients(player, recipe);
            return false;
        }

        // Check if player is in combat
        if (plugin.getCombatManager().isInCombat(player)) {
            player.sendMessage("§cYou cannot craft while in combat!");
            return false;
        }

        // Remove ingredients from inventory
        removeIngredients(player, recipe);

        // Start crafting session
        CraftingSession session = new CraftingSession(recipe, System.currentTimeMillis());
        activeCrafting.put(player.getUniqueId().toString(), session);

        player.sendMessage("§aStarted crafting " + recipe.getDisplayName() + "...");
        player.sendMessage("§eThis will take " + recipe.getCraftingTime() + " seconds to complete.");

        // Start crafting processor
        processCrafting(player, session);

        return true;
    }

    /**
     * Cancels a player's crafting session
     */
    public void cancelCrafting(Player player) {
        String uuid = player.getUniqueId().toString();
        CraftingSession session = activeCrafting.remove(uuid);
        
        if (session != null) {
            // Return ingredients to player
            returnIngredients(player, session.getRecipe());
            player.sendMessage("§cCrafting cancelled. Ingredients returned.");
        }
    }

    /**
     * Processes crafting completion
     */
    private void processCrafting(Player player, CraftingSession session) {
        plugin.getServer().getScheduler().runTaskLater(plugin, () -> {
            String uuid = player.getUniqueId().toString();
            
            // Check if session is still active
            if (!activeCrafting.containsKey(uuid)) {
                return;
            }

            // Check if player is still online
            if (!player.isOnline()) {
                activeCrafting.remove(uuid);
                return;
            }

            // Check if player moved too far or is in combat
            if (plugin.getCombatManager().isInCombat(player)) {
                cancelCrafting(player);
                player.sendMessage("§cCrafting cancelled due to combat!");
                return;
            }

            // Complete crafting
            completeCrafting(player, session);
            activeCrafting.remove(uuid);
            
        }, session.getRecipe().getCraftingTime() * 20L); // Convert seconds to ticks
    }

    /**
     * Completes a crafting session
     */
    private void completeCrafting(Player player, CraftingSession session) {
        CraftingRecipe recipe = session.getRecipe();
        
        // Calculate success
        Random random = new Random();
        int successChance = recipe.getSuccessRate();
        
        // Increase success chance based on player level
        int playerLevel = plugin.getPlayerManager().getPlayerData(player).getExperience() / 100;
        successChance += (playerLevel - recipe.getRequiredLevel()) * 2;
        successChance = Math.min(100, Math.max(10, successChance));
        
        if (random.nextInt(100) < successChance) {
            // Successful crafting
            ItemStack result = new ItemStack(recipe.getResultMaterial(), recipe.getResultAmount());
            
            // Add custom meta if needed
            ItemMeta meta = result.getItemMeta();
            if (meta != null) {
                meta.setDisplayName("§6" + recipe.getDisplayName());
                meta.setLore(Arrays.asList("§7Crafted by: " + player.getName()));
                result.setItemMeta(meta);
            }
            
            // Give result to player
            player.getInventory().addItem(result);
            
            // Grant experience
            int expGain = plugin.getConfig().getInt("crafting.experience_per_craft", 10);
            plugin.getPlayerManager().addExperience(player, expGain);
            
            player.sendMessage("§aSuccessfully crafted " + recipe.getDisplayName() + "!");
            player.sendMessage("§eGained " + expGain + " experience!");
            
        } else {
            // Failed crafting
            player.sendMessage("§cCrafting failed! Better luck next time.");
            
            // Return some ingredients on failure
            returnSomeIngredients(player, recipe);
        }
    }

    /**
     * Checks if player has required ingredients
     */
    private boolean hasRequiredIngredients(Player player, CraftingRecipe recipe) {
        Map<Material, Integer> inventory = new HashMap<>();
        
        // Count player's items
        for (ItemStack item : player.getInventory().getContents()) {
            if (item != null) {
                inventory.put(item.getType(), inventory.getOrDefault(item.getType(), 0) + item.getAmount());
            }
        }
        
        // Check requirements
        for (Map.Entry<Material, Integer> requirement : recipe.getIngredients().entrySet()) {
            if (inventory.getOrDefault(requirement.getKey(), 0) < requirement.getValue()) {
                return false;
            }
        }
        
        return true;
    }

    /**
     * Shows required ingredients to player
     */
    private void showRequiredIngredients(Player player, CraftingRecipe recipe) {
        player.sendMessage("§6Required ingredients for " + recipe.getDisplayName() + ":");
        for (Map.Entry<Material, Integer> ingredient : recipe.getIngredients().entrySet()) {
            player.sendMessage("§7- " + ingredient.getValue() + "x " + ingredient.getKey().toString());
        }
    }

    /**
     * Removes ingredients from player's inventory
     */
    private void removeIngredients(Player player, CraftingRecipe recipe) {
        for (Map.Entry<Material, Integer> ingredient : recipe.getIngredients().entrySet()) {
            removeItemFromInventory(player, ingredient.getKey(), ingredient.getValue());
        }
    }

    /**
     * Returns ingredients to player's inventory
     */
    private void returnIngredients(Player player, CraftingRecipe recipe) {
        for (Map.Entry<Material, Integer> ingredient : recipe.getIngredients().entrySet()) {
            ItemStack item = new ItemStack(ingredient.getKey(), ingredient.getValue());
            player.getInventory().addItem(item);
        }
    }

    /**
     * Returns some ingredients on crafting failure
     */
    private void returnSomeIngredients(Player player, CraftingRecipe recipe) {
        Random random = new Random();
        for (Map.Entry<Material, Integer> ingredient : recipe.getIngredients().entrySet()) {
            int returnAmount = ingredient.getValue() / 2; // Return half
            if (returnAmount > 0 && random.nextBoolean()) {
                ItemStack item = new ItemStack(ingredient.getKey(), returnAmount);
                player.getInventory().addItem(item);
            }
        }
    }

    /**
     * Removes a specific item from player's inventory
     */
    private void removeItemFromInventory(Player player, Material material, int amount) {
        int remaining = amount;
        for (ItemStack item : player.getInventory().getContents()) {
            if (item != null && item.getType() == material) {
                if (item.getAmount() >= remaining) {
                    item.setAmount(item.getAmount() - remaining);
                    remaining = 0;
                    break;
                } else {
                    remaining -= item.getAmount();
                    item.setAmount(0);
                }
            }
        }
    }

    /**
     * Adds a new crafting recipe
     */
    public void addRecipe(CraftingRecipe recipe) {
        recipes.put(recipe.getId(), recipe);
    }

    /**
     * Removes a crafting recipe
     */
    public void removeRecipe(String recipeId) {
        recipes.remove(recipeId);
    }

    /**
     * Gets all available recipes for a player
     */
    public List<CraftingRecipe> getAvailableRecipes(Player player) {
        int playerLevel = plugin.getPlayerManager().getPlayerData(player).getExperience() / 100;
        
        return recipes.values().stream()
                .filter(recipe -> recipe.getRequiredLevel() <= playerLevel)
                .sorted(Comparator.comparingInt(CraftingRecipe::getRequiredLevel))
                .toList();
    }

    // Getters and utility methods
    public CraftingRecipe getRecipe(String id) {
        return recipes.get(id);
    }

    public boolean isPlayerCrafting(Player player) {
        return activeCrafting.containsKey(player.getUniqueId().toString());
    }

    public Map<String, CraftingRecipe> getAllRecipes() {
        return new HashMap<>(recipes);
    }

    // Inner classes
    public static class CraftingRecipe {
        private final String id;
        private final String displayName;
        private final Map<Material, Integer> ingredients = new HashMap<>();
        private Material resultMaterial;
        private int resultAmount = 1;
        private int craftingTime = 30;
        private int requiredLevel = 0;
        private int successRate = 100;

        public CraftingRecipe(String id, String displayName) {
            this.id = id;
            this.displayName = displayName;
        }

        public CraftingRecipe addIngredient(Material material, int amount) {
            ingredients.put(material, amount);
            return this;
        }

        public CraftingRecipe setResult(Material material, int amount) {
            this.resultMaterial = material;
            this.resultAmount = amount;
            return this;
        }

        public CraftingRecipe setCraftingTime(int seconds) {
            this.craftingTime = seconds;
            return this;
        }

        public CraftingRecipe setRequiredLevel(int level) {
            this.requiredLevel = level;
            return this;
        }

        public CraftingRecipe setSuccessRate(int rate) {
            this.successRate = rate;
            return this;
        }

        // Getters
        public String getId() { return id; }
        public String getDisplayName() { return displayName; }
        public Map<Material, Integer> getIngredients() { return ingredients; }
        public Material getResultMaterial() { return resultMaterial; }
        public int getResultAmount() { return resultAmount; }
        public int getCraftingTime() { return craftingTime; }
        public int getRequiredLevel() { return requiredLevel; }
        public int getSuccessRate() { return successRate; }
    }

    private static class CraftingSession {
        private final CraftingRecipe recipe;
        private final long startTime;

        public CraftingSession(CraftingRecipe recipe, long startTime) {
            this.recipe = recipe;
            this.startTime = startTime;
        }

        public CraftingRecipe getRecipe() { return recipe; }
        public long getStartTime() { return startTime; }
    }
}