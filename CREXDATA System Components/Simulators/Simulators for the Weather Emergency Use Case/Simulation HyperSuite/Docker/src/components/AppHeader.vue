<template>
	<header class="app-header">
		<h1>GUI for EmCase Simulator Parameterization</h1>
		<a href="https://crexdata.eu/" target="_blank" rel="noopener noreferrer">
			<img src="@/assets/logo_CREXDATA.svg" alt="Logo" class="header-image" />
		</a>
	</header>
</template>

<script>
	export default {
		name: 'AppHeader',
	};
</script>

<style scoped="">
	.app-header {
		display: flex;
		justify-content: space-between;
		align-items: center;
		padding: 10px 20px;
		color: black;
	}

	.header-image {
		height: 50px;
	}
</style>
