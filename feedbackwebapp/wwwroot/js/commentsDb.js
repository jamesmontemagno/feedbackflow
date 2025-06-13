// Comments-specific IndexedDB operations
// This module handles all comment data storage operations separately from history data

// Comments database configuration
const COMMENTS_CONFIG = {
    dbName: 'feedbackflow-comments-db',
    storeName: 'comments'
};

// Helper functions for comments IndexedDB operations
export {
    // Core CRUD operations
    addComment,
    editComment,
    deleteComment,
    getCommentsByFeedbackId,
    getCommentsByFeedbackIds,
    // Management operations
    deleteCommentsByFeedbackId,
    clearAllComments,
    migrateCommentsFromHistory
};

// Initialize comments database
async function initCommentsDb() {
    return new Promise((resolve, reject) => {
        const request = indexedDB.open(COMMENTS_CONFIG.dbName, 1);

        request.onerror = () => reject(request.error);
        request.onsuccess = () => resolve(request.result);

        request.onupgradeneeded = (event) => {
            const db = event.target.result;
            const oldVersion = event.oldVersion;
            console.log(`Upgrading ${COMMENTS_CONFIG.dbName} from version ${oldVersion} to 1`);

            // Create comments store with commentId as key
            if (!db.objectStoreNames.contains(COMMENTS_CONFIG.storeName)) {
                const store = db.createObjectStore(COMMENTS_CONFIG.storeName, { keyPath: 'commentId' });
                // Create index for feedbackId to efficiently query comments by feedback item
                store.createIndex('feedbackId', 'feedbackId', { unique: false });
                console.log(`Created store ${COMMENTS_CONFIG.storeName} with feedbackId index`);
            }
        };
    });
}

// Add a new comment
async function addComment(feedbackId, comment) {
    const db = await initCommentsDb();
    return new Promise((resolve, reject) => {
        const transaction = db.transaction(COMMENTS_CONFIG.storeName, 'readwrite');
        const store = transaction.objectStore(COMMENTS_CONFIG.storeName);
        
        // Create comment record with required schema
        const commentRecord = {
            commentId: comment.commentId || `${feedbackId}_${Date.now()}_${Math.random().toString(36).substr(2, 9)}_${performance.now()}`,
            feedbackId: feedbackId,
            author: comment.author || '',
            content: comment.content || '',
            timestamp: comment.timestamp || new Date(),
            // Additional metadata from original comment
            parentId: comment.parentId || null,
            createdAt: comment.createdAt || new Date(),
            url: comment.url || null,
            score: comment.score || null,
            metadata: comment.metadata || null,
            // Preserve nested replies if they exist
            replies: comment.replies || []
        };

        const request = store.add(commentRecord);

        request.onerror = () => reject(request.error);
        request.onsuccess = () => resolve(commentRecord);
        transaction.oncomplete = () => db.close();
    });
}

// Edit an existing comment
async function editComment(commentId, updatedComment) {
    const db = await initCommentsDb();
    return new Promise((resolve, reject) => {
        const transaction = db.transaction(COMMENTS_CONFIG.storeName, 'readwrite');
        const store = transaction.objectStore(COMMENTS_CONFIG.storeName);
        
        // First get the existing comment
        const getRequest = store.get(commentId);
        
        getRequest.onsuccess = () => {
            const existingComment = getRequest.result;
            if (!existingComment) {
                reject(new Error(`Comment with ID ${commentId} not found`));
                return;
            }

            // Update the comment with new data while preserving structure
            const updatedRecord = {
                ...existingComment,
                ...updatedComment,
                commentId: commentId, // Ensure ID doesn't change
                timestamp: new Date() // Update timestamp for modification
            };

            const putRequest = store.put(updatedRecord);
            
            putRequest.onerror = () => reject(putRequest.error);
            putRequest.onsuccess = () => resolve(updatedRecord);
        };

        getRequest.onerror = () => reject(getRequest.error);
        transaction.oncomplete = () => db.close();
    });
}

// Delete a specific comment
async function deleteComment(commentId) {
    const db = await initCommentsDb();
    return new Promise((resolve, reject) => {
        const transaction = db.transaction(COMMENTS_CONFIG.storeName, 'readwrite');
        const store = transaction.objectStore(COMMENTS_CONFIG.storeName);
        const request = store.delete(commentId);

        request.onerror = () => reject(request.error);
        request.onsuccess = () => resolve();
        transaction.oncomplete = () => db.close();
    });
}

// Get all comments for a specific feedback item
async function getCommentsByFeedbackId(feedbackId) {
    const db = await initCommentsDb();
    return new Promise((resolve, reject) => {
        const transaction = db.transaction(COMMENTS_CONFIG.storeName, 'readonly');
        const store = transaction.objectStore(COMMENTS_CONFIG.storeName);
        const index = store.index('feedbackId');
        const request = index.getAll(feedbackId);

        request.onerror = () => reject(request.error);
        request.onsuccess = () => resolve(request.result || []);
        transaction.oncomplete = () => db.close();
    });
}

// Get comments for multiple feedback items (bulk operation for export)
async function getCommentsByFeedbackIds(feedbackIds) {
    if (!feedbackIds || feedbackIds.length === 0) {
        return {};
    }

    const db = await initCommentsDb();
    return new Promise((resolve, reject) => {
        const transaction = db.transaction(COMMENTS_CONFIG.storeName, 'readonly');
        const store = transaction.objectStore(COMMENTS_CONFIG.storeName);
        const index = store.index('feedbackId');
        
        const results = {};
        let completedRequests = 0;
        
        // Initialize results object
        feedbackIds.forEach(id => {
            results[id] = [];
        });

        if (feedbackIds.length === 0) {
            resolve(results);
            return;
        }

        feedbackIds.forEach(feedbackId => {
            const request = index.getAll(feedbackId);
            
            request.onsuccess = () => {
                results[feedbackId] = request.result || [];
                completedRequests++;
                
                if (completedRequests === feedbackIds.length) {
                    resolve(results);
                }
            };
            
            request.onerror = () => reject(request.error);
        });

        transaction.oncomplete = () => db.close();
    });
}

// Delete all comments for a specific feedback item (for data consistency)
async function deleteCommentsByFeedbackId(feedbackId) {
    const db = await initCommentsDb();
    return new Promise((resolve, reject) => {
        const transaction = db.transaction(COMMENTS_CONFIG.storeName, 'readwrite');
        const store = transaction.objectStore(COMMENTS_CONFIG.storeName);
        const index = store.index('feedbackId');
        const request = index.getAll(feedbackId);

        request.onsuccess = () => {
            const comments = request.result || [];
            let deletedCount = 0;
            
            if (comments.length === 0) {
                resolve(0);
                return;
            }

            comments.forEach(comment => {
                const deleteRequest = store.delete(comment.commentId);
                deleteRequest.onsuccess = () => {
                    deletedCount++;
                    if (deletedCount === comments.length) {
                        resolve(deletedCount);
                    }
                };
                deleteRequest.onerror = () => reject(deleteRequest.error);
            });
        };

        request.onerror = () => reject(request.error);
        transaction.oncomplete = () => db.close();
    });
}

// Clear all comments (for testing and maintenance)
async function clearAllComments() {
    const db = await initCommentsDb();
    return new Promise((resolve, reject) => {
        const transaction = db.transaction(COMMENTS_CONFIG.storeName, 'readwrite');
        const store = transaction.objectStore(COMMENTS_CONFIG.storeName);
        const request = store.clear();

        request.onerror = () => reject(request.error);
        request.onsuccess = () => resolve();
        transaction.oncomplete = () => db.close();
    });
}

// Migration function to move existing comments from history items to comments database
async function migrateCommentsFromHistory(historyItems) {
    if (!historyItems || historyItems.length === 0) {
        return { migrated: 0, errors: [] };
    }

    const results = { migrated: 0, errors: [] };
    
    try {
        for (const historyItem of historyItems) {
            if (!historyItem.CommentThreads || historyItem.CommentThreads.length === 0) {
                continue;
            }

            // Process each comment thread
            for (const thread of historyItem.CommentThreads) {
                if (!thread.Comments || thread.Comments.length === 0) {
                    continue;
                }

                // Process each comment in the thread
                for (const comment of thread.Comments) {
                    try {
                        // Convert CommentData to comment record format
                        const commentRecord = {
                            feedbackId: historyItem.Id,
                            author: comment.Author || '',
                            content: comment.Content || '',
                            timestamp: comment.CreatedAt ? new Date(comment.CreatedAt) : new Date(),
                            parentId: comment.ParentId || null,
                            createdAt: comment.CreatedAt ? new Date(comment.CreatedAt) : new Date(),
                            url: comment.Url || null,
                            score: comment.Score || null,
                            metadata: comment.Metadata || null,
                            replies: comment.Replies || []
                        };

                        await addComment(historyItem.Id, commentRecord);
                        results.migrated++;
                    } catch (error) {
                        console.error(`Failed to migrate comment for feedback ${historyItem.Id}:`, error);
                        results.errors.push(`Comment migration failed: ${error.message}`);
                    }
                }
            }
        }
    } catch (error) {
        console.error('Migration process failed:', error);
        results.errors.push(`Migration process failed: ${error.message}`);
    }

    return results;
}