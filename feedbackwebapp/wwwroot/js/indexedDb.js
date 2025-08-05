// Helper functions for IndexedDB operations
export {
    initDb,
    getItem,
    getAllItems,
    deleteItem,
    clearStore,
    // History-specific exports
    getHistoryItem,
    getAllHistoryItems,
    deleteHistoryItem,
    clearHistory,
    getHistoryItemsPaged,
    getHistoryItemsCount
};

// Initialize database
async function initDb(dbName, storeName) {
    return new Promise((resolve, reject) => {
        const request = indexedDB.open(dbName, 1);

        request.onerror = () => reject(request.error);
        request.onsuccess = () => resolve(request.result);

        request.onupgradeneeded = (event) => {
            const db = event.target.result;
            const oldVersion = event.oldVersion;
            console.log(`Upgrading ${dbName} from version ${oldVersion} to 1`);

            // Create or get the store
            if (!db.objectStoreNames.contains(storeName)) {
                const store = db.createObjectStore(storeName, { keyPath: 'id' });
                console.log(`Created store ${storeName}`);
            }
        };
    });
}

// Get a single item by id
async function getItem(dbName, storeName, id) {
    const db = await initDb(dbName, storeName);
    return new Promise((resolve, reject) => {
        const transaction = db.transaction(storeName, 'readonly');
        const store = transaction.objectStore(storeName);
        const request = store.get(id);

        request.onerror = () => reject(request.error);
        request.onsuccess = () => resolve(request.result);
        transaction.oncomplete = () => db.close();
    });
}

// Get all items
async function getAllItems(dbName, storeName) {
    const db = await initDb(dbName, storeName);
    return new Promise((resolve, reject) => {
        const transaction = db.transaction(storeName, 'readonly');
        const store = transaction.objectStore(storeName);
        const request = store.getAll();

        request.onerror = () => reject(request.error);
        request.onsuccess = () => resolve(request.result);
        transaction.oncomplete = () => db.close();
    });
}

// Delete an item
async function deleteItem(dbName, storeName, id) {
    const db = await initDb(dbName, storeName);
    return new Promise((resolve, reject) => {
        const transaction = db.transaction(storeName, 'readwrite');
        const store = transaction.objectStore(storeName);
        const request = store.delete(id);

        request.onerror = () => reject(request.error);
        request.onsuccess = () => resolve();
        transaction.oncomplete = () => db.close();
    });
}

// Clear all items from a store
async function clearStore(dbName, storeName) {
    const db = await initDb(dbName, storeName);
    return new Promise((resolve, reject) => {
        const transaction = db.transaction(storeName, 'readwrite');
        const store = transaction.objectStore(storeName);
        const request = store.clear();

        request.onerror = () => reject(request.error);
        request.onsuccess = () => resolve();
        transaction.oncomplete = () => db.close();
    });
}

// History-specific configuration and helper functions
const HISTORY_CONFIG = {
    dbName: 'History',
    storeName: 'HistoryItems'
};

async function getHistoryItem(id) {
    return getItem(
        HISTORY_CONFIG.dbName, 
        HISTORY_CONFIG.storeName, 
        id
    );
}

async function getAllHistoryItems() {
    return getAllItems(
        HISTORY_CONFIG.dbName, 
        HISTORY_CONFIG.storeName
    );
}

async function deleteHistoryItem(id) {
    return deleteItem(
        HISTORY_CONFIG.dbName, 
        HISTORY_CONFIG.storeName, 
        id
    );
}

async function clearHistory() {
    return clearStore(
        HISTORY_CONFIG.dbName, 
        HISTORY_CONFIG.storeName
    );
}

async function getHistoryItemsPaged(skip, take, searchTerm) {
    const db = await initDb(HISTORY_CONFIG.dbName, HISTORY_CONFIG.storeName);
    return new Promise((resolve, reject) => {
        const transaction = db.transaction(HISTORY_CONFIG.storeName, 'readonly');
        const store = transaction.objectStore(HISTORY_CONFIG.storeName);
        const request = store.getAll();

        request.onerror = () => reject(request.error);
        request.onsuccess = () => {
            let items = request.result || [];
            
            // Sort by timestamp (most recent first)
            items.sort((a, b) => new Date(b.timestamp) - new Date(a.timestamp));
            
            // Apply search filter if provided
            if (searchTerm && searchTerm.trim()) {
                const term = searchTerm.trim().toLowerCase();
                items = items.filter(item => 
                    (item.fullAnalysis && item.fullAnalysis.toLowerCase().includes(term)) ||
                    (item.userInput && item.userInput.toLowerCase().includes(term))
                );
            }
            
            // Apply pagination
            const pagedItems = items.slice(skip, skip + take);
            resolve(pagedItems);
        };
        transaction.oncomplete = () => db.close();
    });
}

async function getHistoryItemsCount(searchTerm) {
    const db = await initDb(HISTORY_CONFIG.dbName, HISTORY_CONFIG.storeName);
    return new Promise((resolve, reject) => {
        const transaction = db.transaction(HISTORY_CONFIG.storeName, 'readonly');
        const store = transaction.objectStore(HISTORY_CONFIG.storeName);
        const request = store.getAll();

        request.onerror = () => reject(request.error);
        request.onsuccess = () => {
            let items = request.result || [];
            
            // Apply search filter if provided
            if (searchTerm && searchTerm.trim()) {
                const term = searchTerm.trim().toLowerCase();
                items = items.filter(item => 
                    (item.fullAnalysis && item.fullAnalysis.toLowerCase().includes(term)) ||
                    (item.userInput && item.userInput.toLowerCase().includes(term))
                );
            }
            
            resolve(items.length);
        };
        transaction.oncomplete = () => db.close();
    });
}
