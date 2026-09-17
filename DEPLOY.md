# =============================================
# LendSoft Pro - Deploy Checklist
# =============================================

## 1. BASE DE DATOS

- [ ] Instalar SQL Server 2019+ Express o superior
- [ ] Crear base de datos `LendSoftDB`
- [ ] Crear base de datos `LendSoftHangfire`
- [ ] Ejecutar `script_produccion.sql` en `LendSoftDB`
- [ ] Configurar usuario SQL con permisos db_owner

## 2. CONFIGURACIÓN

- [ ] Copiar `.env.template` a `.env` y llenar valores reales
- [ ] Generar nuevas claves de encriptación (NO usar las del template)
- [ ] Configurar WhatsApp Cloud API en Meta for Developers
- [ ] Obtener PhoneNumberId y AccessToken permanente
- [ ] Crear plantillas de WhatsApp en Meta Business Manager
- [ ] Configurar servidor SMTP para correos

## 3. SERVIDOR (Windows + IIS)

- [ ] Instalar .NET 10 Hosting Bundle
- [ ] Instalar certificado SSL (Let's Encrypt o comercial)
- [ ] Crear Application Pool "Sin código administrado"
- [ ] Crear sitio web apuntando a la carpeta `publicado`
- [ ] Forzar HTTPS (HSTS + redirección 301)
- [ ] Configurar `AllowedHosts` con el dominio real

## 4. VARIABLES DE ENTORNO (en el servidor)

- [ ] `ENCRYPTION_KEY` = (base64, 32 bytes)
- [ ] `JWT_KEY` = (min 32 caracteres aleatorios)
- [ ] `ADMIN_INITIAL_PASSWORD` = (contraseña segura temporal)

## 5. VERIFICACIÓN POST-DEPLOY

- [ ] Acceder a `https://tudominio.com` - debe mostrar login
- [ ] Iniciar sesión con admin@sistema.com / ADMIN_INITIAL_PASSWORD
- [ ] Cambiar contraseña del admin inmediatamente
- [ ] Crear un cliente de prueba
- [ ] Crear un préstamo de prueba
- [ ] Configurar WhatsApp en /Notificaciones/WhatsAppConfig
- [ ] Enviar mensaje de prueba
- [ ] Verificar que /hangfire solo es accesible como Admin
- [ ] Verificar que /api/* devuelve 401 sin autenticación

## 6. RESPALDO

- [ ] Configurar backup diario de SQL Server
- [ ] Guardar `.env` en lugar seguro (gestor de secretos)
- [ ] Documentar credenciales en bóveda de contraseñas
