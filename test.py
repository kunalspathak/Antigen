from bs4 import BeautifulSoup

# Load the HTML file
with open("abs.html", "r", encoding="utf-8") as file:
    html_content = file.read()
    
# Mapping for word replacements
replacement_mapping = {
    'Rm': 'm',
    'Rn': 'n',
    'Rd': 'd',
    'Rt': 't',
    'imm': 'i',
    'immediate': 'i'
}

# Parse the HTML
soup = BeautifulSoup(html_content, 'html.parser')

# Find the table with class "regdiagram"
table = soup.find('table', class_='regdiagram')

# Find all rows in the table
rows = table.find_all('tr')

# Extract data from the second row with word replacements
if len(rows) > 1:
    second_row = rows[1]  # Index 1 corresponds to the second row
    cells = second_row.find_all('td')
    
    # Extract the data from each cell with word replacements
    extracted_data = []
    for cell in cells:
        # Check if the cell spans multiple columns
        if 'colspan' in cell.attrs:
            colspan = int(cell['colspan'])
            extracted_data.extend([replacement_mapping.get('-', '-')] * colspan)
        else:
            cell_text = cell.get_text().strip()
            # Replace words according to the mapping
            for word, replacement in replacement_mapping.items():
                cell_text = cell_text.replace(word, replacement)
            extracted_data.append(cell_text)

    # Print the extracted data with word replacements
    print(extracted_data)
else:
    print("Table does not have enough rows.")
