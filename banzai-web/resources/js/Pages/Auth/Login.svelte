<script lang="ts">
    let { errors, csrf_token } = $props()
</script>

<form method="post" action="/login">
    <input type="hidden" name="_token" value={csrf_token} />
    <h1>Login</h1>

    <div>
        <label for="name">Username</label>
        <input id="name" type="text" name="name" required />
        {#if errors?.name}
            <div>{errors.name}</div>
        {/if}
    </div>

    <div>
        <label for="password">Password</label>
        <input id="password" type="password" name="password" required />
        {#if errors?.password}
            <div>{errors.password}</div>
        {/if}
    </div>

    <div>
        <label>
            <input type="checkbox" name="remember" />
            Remember me
        </label>
    </div>

    <button type="submit">Login</button>

    <p>
        <a href="/register">Register</a>
    </p>
</form>
