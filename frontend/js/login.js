const form = document.getElementById("form-login");
const btnEntrar = document.getElementById("btn-entrar");

if (sesion.vigente) {
    window.location.replace(sesion.leer().paginaInicio);
}

form.addEventListener("submit", entrar);

async function entrar(evento) {
    evento.preventDefault();

    btnEntrar.disabled = true;
    btnEntrar.textContent = "Verificando…";

    try {
        const datos = await api.crear("auth/login", {
            email: form.email.value.trim(),
            password: form.password.value
        });

        sesion.guardar(datos);
        window.location.replace(datos.paginaInicio);
    } catch (error) {
        mostrarAviso(error.message, "error");
        form.password.value = "";
        form.password.focus();
        btnEntrar.disabled = false;
        btnEntrar.textContent = "Entrar";
    }
}
