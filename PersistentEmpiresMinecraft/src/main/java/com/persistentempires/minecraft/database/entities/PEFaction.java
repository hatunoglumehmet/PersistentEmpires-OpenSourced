package com.persistentempires.minecraft.database.entities;

import java.util.ArrayList;
import java.util.List;

/**
 * Database entity representing a faction in the Persistent Empires system
 */
public class PEFaction {
    private int id;
    private String name;
    private String displayName;
    private String bannerPattern;
    private String lordUUID;
    private List<String> marshalls;
    private List<String> doorManagers;
    private List<String> chestManagers;
    private List<Integer> warDeclarations;
    private long pollUnlockedAt;
    private long createdAt;

    // Constructor for new factions
    public PEFaction(int id, String name, String displayName, String lordUUID) {
        this.id = id;
        this.name = name;
        this.displayName = displayName;
        this.bannerPattern = "";
        this.lordUUID = lordUUID;
        this.marshalls = new ArrayList<>();
        this.doorManagers = new ArrayList<>();
        this.chestManagers = new ArrayList<>();
        this.warDeclarations = new ArrayList<>();
        this.pollUnlockedAt = 0;
        this.createdAt = System.currentTimeMillis();
    }

    // Constructor for loading from database
    public PEFaction(int id, String name, String displayName, String bannerPattern, String lordUUID,
                    long pollUnlockedAt, long createdAt) {
        this.id = id;
        this.name = name;
        this.displayName = displayName;
        this.bannerPattern = bannerPattern;
        this.lordUUID = lordUUID;
        this.marshalls = new ArrayList<>();
        this.doorManagers = new ArrayList<>();
        this.chestManagers = new ArrayList<>();
        this.warDeclarations = new ArrayList<>();
        this.pollUnlockedAt = pollUnlockedAt;
        this.createdAt = createdAt;
    }

    // Getters and setters
    public int getId() { return id; }
    public void setId(int id) { this.id = id; }

    public String getName() { return name; }
    public void setName(String name) { this.name = name; }

    public String getDisplayName() { return displayName; }
    public void setDisplayName(String displayName) { this.displayName = displayName; }

    public String getBannerPattern() { return bannerPattern; }
    public void setBannerPattern(String bannerPattern) { this.bannerPattern = bannerPattern; }

    public String getLordUUID() { return lordUUID; }
    public void setLordUUID(String lordUUID) { this.lordUUID = lordUUID; }

    public List<String> getMarshalls() { return marshalls; }
    public void setMarshalls(List<String> marshalls) { this.marshalls = marshalls; }

    public List<String> getDoorManagers() { return doorManagers; }
    public void setDoorManagers(List<String> doorManagers) { this.doorManagers = doorManagers; }

    public List<String> getChestManagers() { return chestManagers; }
    public void setChestManagers(List<String> chestManagers) { this.chestManagers = chestManagers; }

    public List<Integer> getWarDeclarations() { return warDeclarations; }
    public void setWarDeclarations(List<Integer> warDeclarations) { this.warDeclarations = warDeclarations; }

    public long getPollUnlockedAt() { return pollUnlockedAt; }
    public void setPollUnlockedAt(long pollUnlockedAt) { this.pollUnlockedAt = pollUnlockedAt; }

    public long getCreatedAt() { return createdAt; }
    public void setCreatedAt(long createdAt) { this.createdAt = createdAt; }

    // Utility methods
    public boolean isLord(String playerUUID) {
        return lordUUID != null && lordUUID.equals(playerUUID);
    }

    public boolean isMarshall(String playerUUID) {
        return marshalls.contains(playerUUID);
    }

    public boolean isDoorManager(String playerUUID) {
        return doorManagers.contains(playerUUID);
    }

    public boolean isChestManager(String playerUUID) {
        return chestManagers.contains(playerUUID);
    }

    public boolean hasManagementPermission(String playerUUID) {
        return isLord(playerUUID) || isMarshall(playerUUID);
    }

    public boolean canManageDoors(String playerUUID) {
        return hasManagementPermission(playerUUID) || isDoorManager(playerUUID);
    }

    public boolean canManageChests(String playerUUID) {
        return hasManagementPermission(playerUUID) || isChestManager(playerUUID);
    }

    public boolean isAtWarWith(int factionId) {
        return warDeclarations.contains(factionId);
    }

    public void declareWar(int factionId) {
        if (!warDeclarations.contains(factionId)) {
            warDeclarations.add(factionId);
        }
    }

    public void makePeace(int factionId) {
        warDeclarations.remove(Integer.valueOf(factionId));
    }

    public void addMarshall(String playerUUID) {
        if (!marshalls.contains(playerUUID)) {
            marshalls.add(playerUUID);
        }
    }

    public void removeMarshall(String playerUUID) {
        marshalls.remove(playerUUID);
    }

    public void addDoorManager(String playerUUID) {
        if (!doorManagers.contains(playerUUID)) {
            doorManagers.add(playerUUID);
        }
    }

    public void removeDoorManager(String playerUUID) {
        doorManagers.remove(playerUUID);
    }

    public void addChestManager(String playerUUID) {
        if (!chestManagers.contains(playerUUID)) {
            chestManagers.add(playerUUID);
        }
    }

    public void removeChestManager(String playerUUID) {
        chestManagers.remove(playerUUID);
    }

    public boolean isPollUnlocked() {
        return System.currentTimeMillis() >= pollUnlockedAt;
    }

    public void lockPoll(long durationMillis) {
        this.pollUnlockedAt = System.currentTimeMillis() + durationMillis;
    }

    public int getMemberCount() {
        // This would be calculated dynamically by querying the database
        return 0;
    }

    @Override
    public String toString() {
        return "PEFaction{" +
                "id=" + id +
                ", name='" + name + '\'' +
                ", displayName='" + displayName + '\'' +
                ", lordUUID='" + lordUUID + '\'' +
                ", marshalls=" + marshalls.size() +
                ", warDeclarations=" + warDeclarations.size() +
                '}';
    }
}