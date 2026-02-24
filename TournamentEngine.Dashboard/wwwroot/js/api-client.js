/**
 * API Client Service
 * Handles all API calls to the Tournament Engine backend
 */

class ApiClient {
    constructor() {
        this.timeout = 15000; // 15 seconds - generous to handle heavy tournament load
        this.retryAttempts = 2;
    }

    /**
     * Make a generic GET request with error handling
     */
    async get(url) {
        const controller = new AbortController();
        const timeoutId = setTimeout(() => controller.abort(), this.timeout);

        try {
            const response = await fetch(url, {
                method: 'GET',
                headers: {
                    'Accept': 'application/json'
                },
                signal: controller.signal
            });

            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }

            return await response.json();
        } catch (error) {
            if (error?.name === 'AbortError') {
                const timeoutError = new Error(`Request timed out after ${this.timeout}ms`);
                timeoutError.name = 'TimeoutError';
                timeoutError.url = url;
                timeoutError.isTimeout = true;
                throw timeoutError;
            }

            console.error(`API Error (${url}):`, error);
            throw error;
        } finally {
            clearTimeout(timeoutId);
        }
    }

    /**
     * Get tournament status
     */
    async getStatus() {
        return this.get(ENDPOINTS.STATUS);
    }

    /**
     * Get list of all tournament events
     */
    async getEvents() {
        return this.get(ENDPOINTS.EVENTS);
    }

    /**
     * Get overall leaders/leaderboard
     */
    async getLeaders() {
        return this.get(ENDPOINTS.LEADERS);
    }

    /**
     * Get connection status
     */
    async getConnection() {
        return this.get(ENDPOINTS.CONNECTION);
    }

    /**
     * Get groups for a specific event
     */
    async getGroups(eventName) {
        const encodedEventName = encodeURIComponent(eventName);
        return this.get(ENDPOINTS.GROUPS(encodedEventName));
    }

    /**
     * Get group details (standings and matches) for a specific event and group
     */
    async getGroupDetails(eventName, groupLabel) {
        const encodedEventName = encodeURIComponent(eventName);
        const encodedGroupLabel = encodeURIComponent(groupLabel);
        return this.get(ENDPOINTS.GROUP_DETAILS(encodedEventName, encodedGroupLabel));
    }

    /**
     * Get all matches
     */
    async getMatches() {
        return this.get(ENDPOINTS.MATCHES);
    }
}

// Export as global
const apiClient = new ApiClient();
