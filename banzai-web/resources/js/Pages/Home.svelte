<script lang="ts">
    let { auth, csrf_token } = $props()
</script>

<h1>osu!banzai</h1>

{#if auth?.user}
    <p>Welcome, {auth.user.name}</p>
    <form method="post" action="/logout">
        <input type="hidden" name="_token" value={csrf_token} />
        <button type="submit">Logout</button>
    </form>
{:else}
    <a href="/login">Login</a>
    <a href="/register">Register</a>
{/if}
