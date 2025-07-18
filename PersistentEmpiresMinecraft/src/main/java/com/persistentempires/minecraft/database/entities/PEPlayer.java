package com.persistentempires.minecraft.database.entities;

/**
 * Database entity representing a player in the Persistent Empires system
 */
public class PEPlayer {
    private String playerUUID;
    private String playerName;
    private int factionId;
    private int gold;
    private int bankAmount;
    private int hunger;
    private int health;
    private int experience;
    private String playerClass;
    private double posX;
    private double posY;
    private double posZ;
    private String world;
    private String customName;
    private long lastLogin;
    private long combatLogUntil;
    private long woundedUntil;
    private long createdAt;

    // Constructor for new players
    public PEPlayer(String playerUUID, String playerName) {
        this.playerUUID = playerUUID;
        this.playerName = playerName;
        this.factionId = 0;
        this.gold = 1000;
        this.bankAmount = 0;
        this.hunger = 100;
        this.health = 20;
        this.experience = 0;
        this.playerClass = "peasant";
        this.posX = 0;
        this.posY = 0;
        this.posZ = 0;
        this.world = "world";
        this.customName = "";
        this.lastLogin = System.currentTimeMillis();
        this.combatLogUntil = 0;
        this.woundedUntil = 0;
        this.createdAt = System.currentTimeMillis();
    }

    // Constructor for loading from database
    public PEPlayer(String playerUUID, String playerName, int factionId, int gold, int bankAmount, 
                   int hunger, int health, int experience, String playerClass, double posX, double posY, 
                   double posZ, String world, String customName, long lastLogin, long combatLogUntil, 
                   long woundedUntil, long createdAt) {
        this.playerUUID = playerUUID;
        this.playerName = playerName;
        this.factionId = factionId;
        this.gold = gold;
        this.bankAmount = bankAmount;
        this.hunger = hunger;
        this.health = health;
        this.experience = experience;
        this.playerClass = playerClass;
        this.posX = posX;
        this.posY = posY;
        this.posZ = posZ;
        this.world = world;
        this.customName = customName;
        this.lastLogin = lastLogin;
        this.combatLogUntil = combatLogUntil;
        this.woundedUntil = woundedUntil;
        this.createdAt = createdAt;
    }

    // Getters and setters
    public String getPlayerUUID() { return playerUUID; }
    public void setPlayerUUID(String playerUUID) { this.playerUUID = playerUUID; }

    public String getPlayerName() { return playerName; }
    public void setPlayerName(String playerName) { this.playerName = playerName; }

    public int getFactionId() { return factionId; }
    public void setFactionId(int factionId) { this.factionId = factionId; }

    public int getGold() { return gold; }
    public void setGold(int gold) { this.gold = gold; }

    public int getBankAmount() { return bankAmount; }
    public void setBankAmount(int bankAmount) { this.bankAmount = bankAmount; }

    public int getHunger() { return hunger; }
    public void setHunger(int hunger) { this.hunger = Math.max(0, Math.min(100, hunger)); }

    public int getHealth() { return health; }
    public void setHealth(int health) { this.health = Math.max(1, Math.min(20, health)); }

    public int getExperience() { return experience; }
    public void setExperience(int experience) { this.experience = experience; }

    public String getPlayerClass() { return playerClass; }
    public void setPlayerClass(String playerClass) { this.playerClass = playerClass; }

    public double getPosX() { return posX; }
    public void setPosX(double posX) { this.posX = posX; }

    public double getPosY() { return posY; }
    public void setPosY(double posY) { this.posY = posY; }

    public double getPosZ() { return posZ; }
    public void setPosZ(double posZ) { this.posZ = posZ; }

    public String getWorld() { return world; }
    public void setWorld(String world) { this.world = world; }

    public String getCustomName() { return customName; }
    public void setCustomName(String customName) { this.customName = customName; }

    public long getLastLogin() { return lastLogin; }
    public void setLastLogin(long lastLogin) { this.lastLogin = lastLogin; }

    public long getCombatLogUntil() { return combatLogUntil; }
    public void setCombatLogUntil(long combatLogUntil) { this.combatLogUntil = combatLogUntil; }

    public long getWoundedUntil() { return woundedUntil; }
    public void setWoundedUntil(long woundedUntil) { this.woundedUntil = woundedUntil; }

    public long getCreatedAt() { return createdAt; }
    public void setCreatedAt(long createdAt) { this.createdAt = createdAt; }

    // Utility methods
    public boolean isInCombat() {
        return System.currentTimeMillis() < combatLogUntil;
    }

    public boolean isWounded() {
        return System.currentTimeMillis() < woundedUntil;
    }

    public boolean isInFaction() {
        return factionId > 0;
    }

    public void addGold(int amount) {
        this.gold += amount;
    }

    public boolean removeGold(int amount) {
        if (gold >= amount) {
            gold -= amount;
            return true;
        }
        return false;
    }

    public void addBankAmount(int amount) {
        this.bankAmount += amount;
    }

    public boolean removeBankAmount(int amount) {
        if (bankAmount >= amount) {
            bankAmount -= amount;
            return true;
        }
        return false;
    }

    public void addExperience(int amount) {
        this.experience += amount;
    }

    public void reduceHunger(int amount) {
        setHunger(hunger - amount);
    }

    public void restoreHunger(int amount) {
        setHunger(hunger + amount);
    }

    public void setCombatLog(int durationSeconds) {
        this.combatLogUntil = System.currentTimeMillis() + (durationSeconds * 1000L);
    }

    public void setWounded(int durationSeconds) {
        this.woundedUntil = System.currentTimeMillis() + (durationSeconds * 1000L);
    }

    @Override
    public String toString() {
        return "PEPlayer{" +
                "playerUUID='" + playerUUID + '\'' +
                ", playerName='" + playerName + '\'' +
                ", factionId=" + factionId +
                ", gold=" + gold +
                ", bankAmount=" + bankAmount +
                ", hunger=" + hunger +
                ", health=" + health +
                ", experience=" + experience +
                ", playerClass='" + playerClass + '\'' +
                ", customName='" + customName + '\'' +
                '}';
    }
}