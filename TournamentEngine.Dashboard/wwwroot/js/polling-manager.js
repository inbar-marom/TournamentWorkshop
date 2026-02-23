/**
 * Polling Manager
 * Centralized management of all polling intervals
 * Prevents duplicate polls and manages lifecycle
 */

class PollingManager {
    constructor() {
        this.intervals = {};
        this.lastResponses = {};
    }

    /**
     * Start a polling interval with given callback
     * @param {string} name - Unique identifier for this poll
     * @param {Function} callback - Function to call on each poll
     * @param {number} interval - Interval in milliseconds
     * @param {boolean} immediate - Call callback immediately before first interval
     */
    start(name, callback, interval, immediate = false) {
        // Clear existing interval if it exists
        if (this.intervals[name]) {
            clearInterval(this.intervals[name]);
        }

        // Call immediately if requested
        if (immediate) {
            callback().catch(error => console.error(`Error in immediate poll ${name}:`, error));
        }

        // Set up recurring interval
        this.intervals[name] = setInterval(() => {
            callback().catch(error => console.error(`Error in poll ${name}:`, error));
        }, interval);

        console.log(`[Polling] Started: ${name} (${interval}ms)`);
    }

    /**
     * Stop a specific polling interval
     */
    stop(name) {
        if (this.intervals[name]) {
            clearInterval(this.intervals[name]);
            delete this.intervals[name];
            console.log(`[Polling] Stopped: ${name}`);
        }
    }

    /**
     * Stop all polling intervals
     */
    stopAll() {
        Object.keys(this.intervals).forEach(name => this.stop(name));
        console.log(`[Polling] Stopped all intervals`);
    }

    /**
     * Pause a specific interval (clear it temporarily)
     */
    pause(name) {
        this.stop(name);
    }

    /**
     * Resume a specific interval
     */
    resume(name, callback, interval, immediate = false) {
        this.start(name, callback, interval, immediate);
    }

    /**
     * Cache the last response for a poll
     */
    cacheResponse(name, data) {
        this.lastResponses[name] = {
            data: data,
            timestamp: Date.now()
        };
    }

    /**
     * Get cached response
     */
    getCachedResponse(name) {
        return this.lastResponses[name];
    }

    /**
     * Check if response has changed from cached version
     */
    hasChanged(name, newData) {
        const cached = this.lastResponses[name];
        if (!cached) return true;

        // Simple comparison using JSON serialization
        return JSON.stringify(cached.data) !== JSON.stringify(newData);
    }

    /**
     * Get list of active polls
     */
    getActive() {
        return Object.keys(this.intervals);
    }

    /**
     * Log current polling status
     */
    logStatus() {
        const active = this.getActive();
        console.log(`[Polling] Active polls: ${active.length > 0 ? active.join(', ') : 'none'}`);
    }
}

// Export as global
const pollingManager = new PollingManager();
