<script lang="ts">
    let { errors, csrf_token } = $props()
</script>

<form method="post" action="/register">
    <input type="hidden" name="_token" value={csrf_token} />
    <h1>Register</h1>

    <div>
        <label for="name">Username</label>
        <input id="name" type="text" name="name" required />
        {#if errors?.name}
            <div>{errors.name}</div>
        {/if}
    </div>

    <div>
        <label for="email">Email</label>
        <input id="email" type="email" name="email" required />
        {#if errors?.email}
            <div>{errors.email}</div>
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
        <label for="password_confirmation">Confirm Password</label>
        <input id="password_confirmation" type="password" name="password_confirmation" required />
    </div>

    <button type="submit">Register</button>

    <p>
        <a href="/login">Login</a>
    </p>
</form>
