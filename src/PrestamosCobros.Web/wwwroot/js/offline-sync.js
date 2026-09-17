/**
 * offline-sync.js
 * Maneja la persistencia offline de pagos utilizando IndexedDB
 * y sincroniza automáticamente con la API al recuperar la conexión.
 */

const DB_NAME = 'CrediGestOfflineDB';
const STORE_NAME = 'pending_payments';

// Inicializar IndexedDB
function initDB() {
    return new Promise((resolve, reject) => {
        const request = indexedDB.open(DB_NAME, 1);
        request.onupgradeneeded = (event) => {
            const db = event.target.result;
            if (!db.objectStoreNames.contains(STORE_NAME)) {
                db.createObjectStore(STORE_NAME, { keyPath: 'id', autoIncrement: true });
            }
        };
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
    });
}

// Guardar pago localmente cuando no hay conexión
async function savePaymentOffline(paymentData) {
    try {
        const db = await initDB();
        const transaction = db.transaction(STORE_NAME, 'readwrite');
        const store = transaction.objectStore(STORE_NAME);
        
        // Añadimos timestamp para saber cuándo se realizó la operación offline
        const record = { ...paymentData, offlineTimestamp: new Date().toISOString() };
        
        return new Promise((resolve, reject) => {
            const req = store.add(record);
            req.onsuccess = () => resolve(true);
            req.onerror = () => reject(false);
        });
    } catch (error) {
        console.error('Error guardando pago offline:', error);
        return false;
    }
}

// Obtener todos los pagos pendientes
async function getPendingPayments() {
    try {
        const db = await initDB();
        return new Promise((resolve, reject) => {
            const transaction = db.transaction(STORE_NAME, 'readonly');
            const store = transaction.objectStore(STORE_NAME);
            const request = store.getAll();
            request.onsuccess = () => resolve(request.result);
            request.onerror = () => reject(request.error);
        });
    } catch (error) {
        console.error('Error leyendo pagos offline:', error);
        return [];
    }
}

// Eliminar un pago ya sincronizado
async function deletePendingPayment(id) {
    const db = await initDB();
    return new Promise((resolve, reject) => {
        const transaction = db.transaction(STORE_NAME, 'readwrite');
        const store = transaction.objectStore(STORE_NAME);
        const request = store.delete(id);
        request.onsuccess = () => resolve(true);
        request.onerror = () => reject(request.error);
    });
}

// Proceso de sincronización con la API
async function syncOfflineData() {
    console.log('Iniciando sincronización de datos offline...');
    const pendingPayments = await getPendingPayments();
    
    if (pendingPayments.length === 0) {
        console.log('No hay pagos pendientes por sincronizar.');
        return;
    }

    console.log(`Encontrados ${pendingPayments.length} pagos pendientes.`);
    
    for (const payment of pendingPayments) {
        try {
            // Se envía a la API usando FormData o JSON según el backend.
            // Asumiendo envío como Form (ya que se usa un <form> post normalmente)
            const formData = new URLSearchParams();
            formData.append('prestamoId', payment.prestamoId);
            formData.append('CuotaId', payment.CuotaId);
            formData.append('MontoPagado', payment.MontoPagado);
            // Antiforgery token si es necesario
            if (payment.__RequestVerificationToken) {
                formData.append('__RequestVerificationToken', payment.__RequestVerificationToken);
            }

            const response = await fetch('/Prestamos/RegistrarPago', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/x-www-form-urlencoded'
                },
                body: formData.toString()
            });

            if (response.ok || response.redirected) {
                console.log(`Pago ${payment.id} sincronizado exitosamente.`);
                await deletePendingPayment(payment.id);
            } else {
                console.warn(`Error al sincronizar pago ${payment.id}. Status: ${response.status}`);
            }
        } catch (error) {
            console.error(`Fallo de red al intentar sincronizar pago ${payment.id}:`, error);
            // Detenemos la sincronización si la red vuelve a caer
            break;
        }
    }
}

// Escuchar el evento online del navegador para disparar la sincronización
window.addEventListener('online', () => {
    console.log('Conexión recuperada. Intentando sincronizar...');
    syncOfflineData();
});

// Interceptar formularios de pago para manejar la lógica offline
document.addEventListener('DOMContentLoaded', () => {
/*
    const formPago = document.querySelector('form[action="/Prestamos/RegistrarPago"]');
    if (formPago) {
        formPago.addEventListener('submit', async (e) => {
            if (!navigator.onLine) {
                e.preventDefault(); // Evitamos el submit real si no hay red
                
                const formData = new FormData(formPago);
                const paymentData = Object.fromEntries(formData.entries());
                
                const saved = await savePaymentOffline(paymentData);
                if (saved) {
                    alert('Sin conexión: El pago se ha guardado localmente y se sincronizará cuando recupere la red.');
                    const modal = bootstrap.Modal.getInstance(document.getElementById('modalPago'));
                    if (modal) modal.hide();
                    
                    // Actualizar UI para reflejar el estado (opcional)
                    location.reload(); // Recargar para ver los cambios (que en offline podrían requerir lógica extra)
                } else {
                    alert('Error al intentar guardar el pago offline.');
                }
            }
            // Si hay red, dejamos que el formulario se envíe normalmente
        });
    }
*/
});
