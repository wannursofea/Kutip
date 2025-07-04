// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// Sidebar toggle functionality
$(document).ready(() => {
  $("#sidebarCollapse").on("click", () => {
    $("#sidebar").toggleClass("active")
    $("#content").toggleClass("content-with-sidebar content-full")
  })

  // Initialize DataTables if present
  if ($.fn.DataTable) {
    $(".table").DataTable({
      pageLength: 10,
      responsive: true,
      language: {
        search: "Search:",
        lengthMenu: "Show _MENU_ entries",
        info: "Showing _START_ to _END_ of _TOTAL_ entries",
        paginate: {
          first: "First",
          last: "Last",
          next: "Next",
          previous: "Previous",
        },
      },
    })
  }

  // Add smooth animations
  $(".dashboard-card").hover(
    function () {
      $(this).addClass("shadow-lg")
    },
    function () {
      $(this).removeClass("shadow-lg")
    },
  )
})
