from bs4 import BeautifulSoup

# Load the HTML file
with open("abs.html", "r", encoding="utf-8") as file:
    html_content = file.read()
    
# Parse the HTML
soup = BeautifulSoup(html_content, 'html.parser')

# Find the table with class "regdiagram"
table = soup.find('table', class_='regdiagram')

# Find all rows in the table
rows = table.find_all('tr')

# Extract data from the second row
if len(rows) > 1:
    second_row = rows[1]  # Index 1 corresponds to the second row
    cells = second_row.find_all('td')
    
    # Extract the data from each cell
    extracted_data = []
    for cell in cells:
        # Check if the cell spans multiple columns
        if 'colspan' in cell.attrs:
            colspan = int(cell['colspan'])
            extracted_data.extend(['-'] * colspan)
        else:
            extracted_data.append(cell.get_text().strip())

    # Print the extracted data
    print(extracted_data)
else:
    print("Table does not have enough rows.")