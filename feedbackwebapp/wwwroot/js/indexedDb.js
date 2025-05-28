// Helper functions for IndexedDB operations
export {
    initDb,
    getItem,
    saveItem,
    getAllItems,
    deleteItem,
    clearStore,
    migrateFromLocalStorage,
    // History-specific exports
    getHistoryItem,
    saveHistoryItem,
    getAllHistoryItems,
    deleteHistoryItem,
    clearHistory
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

// Add or update an item
async function saveItem(dbName, storeName, item) {
    const db = await initDb(dbName, storeName);
    return new Promise((resolve, reject) => {
        const transaction = db.transaction(storeName, 'readwrite');
        const store = transaction.objectStore(storeName);
        const request = store.put(item);

        request.onerror = () => reject(request.error);
        request.onsuccess = () => {
            const isUpdate = request.result === item.id;
            resolve({ isUpdate, item });
        };
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

// Migrate from localStorage
async function migrateFromLocalStorage(dbName, storeName, key) {
    const data = localStorage.getItem(key);
    if (!data) return;

    try {
        const items = JSON.parse(data);
        if (Array.isArray(items)) {
            const db = await initDb(dbName, storeName);
            const transaction = db.transaction(storeName, 'readwrite');
            const store = transaction.objectStore(storeName);

            for (const item of items) {
                await store.put(item);
            }

            // Remove from localStorage after successful migration
            localStorage.removeItem(key);
        }
    } catch (error) {
        console.error('Migration failed:', error);
    }
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

async function saveHistoryItem(item) {
    return saveItem(
        HISTORY_CONFIG.dbName, 
        HISTORY_CONFIG.storeName, 
        item
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
