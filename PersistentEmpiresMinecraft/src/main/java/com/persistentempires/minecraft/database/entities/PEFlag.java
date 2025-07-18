package com.persistentempires.minecraft.database.entities;

/**
 * Database entity representing a flag/banner that can be captured
 */
public class PEFlag {
    private int id;
    private String name;
    private String world;
    private int x;
    private int y;
    private int z;
    private int factionId;
    private int captureRadius;
    private int captureDuration;
    private long lastCapturedAt;
    private String lastCapturedBy;
    private long createdAt;

    // Constructor for new flags
    public PEFlag(int id, String name, String world, int x, int y, int z) {
        this.id = id;
        this.name = name;
        this.world = world;
        this.x = x;
        this.y = y;
        this.z = z;
        this.factionId = 0;
        this.captureRadius = 3;
        this.captureDuration = 10;
        this.lastCapturedAt = 0;
        this.lastCapturedBy = "";
        this.createdAt = System.currentTimeMillis();
    }

    // Constructor for loading from database
    public PEFlag(int id, String name, String world, int x, int y, int z, int factionId,
                 int captureRadius, int captureDuration, long lastCapturedAt, String lastCapturedBy,
                 long createdAt) {
        this.id = id;
        this.name = name;
        this.world = world;
        this.x = x;
        this.y = y;
        this.z = z;
        this.factionId = factionId;
        this.captureRadius = captureRadius;
        this.captureDuration = captureDuration;
        this.lastCapturedAt = lastCapturedAt;
        this.lastCapturedBy = lastCapturedBy;
        this.createdAt = createdAt;
    }

    // Getters and setters
    public int getId() { return id; }
    public void setId(int id) { this.id = id; }

    public String getName() { return name; }
    public void setName(String name) { this.name = name; }

    public String getWorld() { return world; }
    public void setWorld(String world) { this.world = world; }

    public int getX() { return x; }
    public void setX(int x) { this.x = x; }

    public int getY() { return y; }
    public void setY(int y) { this.y = y; }

    public int getZ() { return z; }
    public void setZ(int z) { this.z = z; }

    public int getFactionId() { return factionId; }
    public void setFactionId(int factionId) { this.factionId = factionId; }

    public int getCaptureRadius() { return captureRadius; }
    public void setCaptureRadius(int captureRadius) { this.captureRadius = captureRadius; }

    public int getCaptureDuration() { return captureDuration; }
    public void setCaptureDuration(int captureDuration) { this.captureDuration = captureDuration; }

    public long getLastCapturedAt() { return lastCapturedAt; }
    public void setLastCapturedAt(long lastCapturedAt) { this.lastCapturedAt = lastCapturedAt; }

    public String getLastCapturedBy() { return lastCapturedBy; }
    public void setLastCapturedBy(String lastCapturedBy) { this.lastCapturedBy = lastCapturedBy; }

    public long getCreatedAt() { return createdAt; }
    public void setCreatedAt(long createdAt) { this.createdAt = createdAt; }

    // Utility methods
    public boolean isNeutral() {
        return factionId == 0;
    }

    public boolean isOwnedBy(int factionId) {
        return this.factionId == factionId;
    }

    public void capture(int newFactionId, String capturedBy) {
        this.factionId = newFactionId;
        this.lastCapturedAt = System.currentTimeMillis();
        this.lastCapturedBy = capturedBy;
    }

    public boolean isInRange(double playerX, double playerY, double playerZ) {
        double distance = Math.sqrt(
            Math.pow(x - playerX, 2) + 
            Math.pow(y - playerY, 2) + 
            Math.pow(z - playerZ, 2)
        );
        return distance <= captureRadius;
    }

    @Override
    public String toString() {
        return "PEFlag{" +
                "id=" + id +
                ", name='" + name + '\'' +
                ", world='" + world + '\'' +
                ", x=" + x +
                ", y=" + y +
                ", z=" + z +
                ", factionId=" + factionId +
                ", captureRadius=" + captureRadius +
                ", captureDuration=" + captureDuration +
                '}';
    }
}