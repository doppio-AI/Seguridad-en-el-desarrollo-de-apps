# Preguntas de reflexión — SonarQube en Docker + .NET Core 10

**1. ¿Qué diferencia hay entre un Security Hotspot y un Bug en SonarQube?**

Un **Bug** es un defecto de comportamiento: código que probablemente no hace lo que el
desarrollador pretendía (una condición siempre verdadera, una excepción no manejada, una
comparación incorrecta). Su impacto es sobre la **corrección funcional** del programa.

Un **Security Hotspot** es distinto: es código que **no es incorrecto por sí mismo**, pero que
toca un punto sensible de seguridad (uso de criptografía, manejo de credenciales, deserialización,
etc.) y que **requiere revisión humana** para decidir si en ese contexto específico representa un
riesgo real o no. SonarQube no puede determinarlo automáticamente porque depende del contexto de
uso — por eso un hotspot se marca como "Reviewed" o "Safe" en vez de "Fixed", ya que a veces la
acción correcta es simplemente confirmar que el uso es seguro, no cambiar el código (como pasó con
nuestros hallazgos de `PasswordHash`, que marcamos como falso positivo en vez de "corregirlos").

**2. ¿Por qué es inseguro concatenar directamente el input del usuario en una query SQL? Ejemplo de payload.**

Concatenar convierte el input del usuario en **parte de la sintaxis SQL** que el motor de base de
datos interpreta, en vez de tratarlo únicamente como un valor de dato. Esto permite que el
atacante "escape" del contexto de dato esperado e inyecte sus propias instrucciones SQL.

Ejemplo clásico — una consulta construida así:
```csharp
var query = $"SELECT * FROM Users WHERE Username = '{username}' AND Password = '{password}'";
```
Si el atacante envía como `username`:
```
' OR '1'='1
```
la consulta resultante queda:
```sql
SELECT * FROM Users WHERE Username = '' OR '1'='1' AND Password = '...'
```
La condición `'1'='1'` siempre es verdadera, por lo que la consulta devuelve el primer usuario de
la tabla sin necesidad de conocer ninguna contraseña real — un bypass de autenticación completo.
La solución es usar **consultas parametrizadas** (o, como en nuestro caso, un ORM como Entity
Framework Core con LINQ), donde el valor del usuario nunca se interpreta como sintaxis SQL, solo
como dato.

**3. ¿Qué es el Code Coverage y por qué SonarQube lo considera relevante para la seguridad?**

Code Coverage mide qué porcentaje de las líneas/ramas del código son ejecutadas al menos una vez
por las pruebas automatizadas. En nuestro proyecto salió **0%** porque no existen pruebas
unitarias.

Su relevancia para seguridad es indirecta pero real: código sin pruebas es código donde **nadie
verifica sistemáticamente el comportamiento ante casos límite o maliciosos** (inputs vacíos,
extremadamente largos, con caracteres especiales, condiciones de carrera, etc.) — precisamente el
tipo de entrada que suele disparar vulnerabilidades. Cobertura alta no garantiza ausencia de
vulnerabilidades, pero cobertura baja o nula significa que cualquier regresión de seguridad
introducida por un cambio futuro pasará desapercibida hasta que llegue a producción, en vez de
detectarse en un pipeline de CI.

**4. Si un colega sube el token de SonarQube a GitHub por error, ¿qué pasos seguirías?**

Esto ocurrió literalmente durante esta práctica (el token quedó en `.env` y fue detectado por el
propio SonarQube como hallazgo Blocker), así que la respuesta es el procedimiento que aplicamos:

1. **Revocar el token inmediatamente** desde SonarQube (My Account → Security → Revoke) — esto lo
   invalida al instante, sin importar dónde haya quedado expuesto.
2. **Generar un token nuevo** para reemplazarlo.
3. **Eliminar el archivo del control de versiones** (`git rm --cached .env`) y agregarlo a
   `.gitignore` para que no se vuelva a subir.
4. **Evaluar si es necesario purgar el historial de Git** (con `git filter-repo` o BFG
   Repo-Cleaner) — si el repo es público o compartido, el token sigue siendo recuperable en
   commits antiguos aunque ya no esté en el HEAD actual, así que revocar (paso 1) es la mitigación
   real; limpiar el historial es higiene adicional.
5. **Notificar al equipo** para que nadie más siga usando el token viejo, y revisar si hubo uso
   no autorizado del token entre el momento de la fuga y la revocación (logs de acceso en
   SonarQube, si están disponibles).
6. Como práctica preventiva a futuro: usar un gestor de secretos (Azure Key Vault, GitHub Secrets,
   variables de entorno inyectadas por el pipeline de CI) en vez de archivos `.env` versionables,
   y agregar un hook de pre-commit o un scanner de secretos (como el que trae SonarQube, o
   `gitleaks`) que bloquee el commit antes de que el secreto llegue siquiera al repo remoto.

**5. ¿Cómo integrarías SonarQube en un pipeline CI/CD de GitHub Actions?**

A alto nivel:

1. **Job de análisis** que se dispare en cada push/PR, con pasos: checkout del código → setup de
   .NET SDK → instalar `dotnet-sonarscanner` → `begin` → `build` → `test` con cobertura → `end`.
2. El **token de SonarQube** se almacena como **GitHub Secret** (`SONAR_TOKEN`), nunca hardcodeado
   en el workflow — se referencia como `${{ secrets.SONAR_TOKEN }}`.
3. Configurar el **Quality Gate** de SonarQube para que el job falle (`exit code != 0`) si el
   análisis no pasa el gate — esto bloquea automáticamente un PR con vulnerabilidades nuevas o
   cobertura insuficiente antes de que se pueda mergear a `main`.
4. Usar la acción oficial `sonarsource/sonarqube-scan-action` o el wrapper de `dotnet-sonarscanner`
   dentro de un step de `run:`, apuntando al `sonar.host.url` de la instancia (que en un entorno
   real sería un servidor accesible desde los runners de GitHub Actions, no `localhost` como en
   esta práctica local).
5. Opcional pero recomendable: agregar un job separado de **decoración de Pull Requests**, donde
   SonarQube comenta directamente en el PR los nuevos hallazgos introducidos por ese cambio
   específico ("New Code" en vez de todo el histórico), facilitando la revisión de código.
