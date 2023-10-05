from bs4 import BeautifulSoup
import re

# Load the HTML file
with open("abs.html", "r", encoding="utf-8") as file:
    html_content = file.read()
    
# Mapping for word replacements (case-sensitive)
replacement_mapping = {
    'Rm': 'm',
    'Rn': 'n',
    'Rd': 'd',
    'Rt': 't',
    'imm|immediate': 'i'
}

# Create a regex pattern for word replacements
pattern = re.compile('|'.join(r'\b{}\b'.format(re.escape(word)) for word in replacement_mapping.keys()))

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
    second_output_data = []  # For the second output
    for cell in cells:
        # Check if the cell spans multiple columns
        if 'colspan' in cell.attrs:
            colspan = int(cell['colspan'])
            cell_text = cell.get_text().strip()
            # Replace words using the regex pattern for the entire cell content
            cell_text = pattern.sub(lambda x: replacement_mapping.get(x.group(0), x.group(0)), cell_text)
            extracted_data.extend([cell_text] * colspan)
            # Replace non-numeric output with 0 for the second output
            second_output_data.extend(['0' if char not in ['0', '1'] else char for char in cell_text] * colspan)
        else:
            cell_text = cell.get_text().strip()
            # Replace words using the regex pattern
            cell_text = pattern.sub(lambda x: replacement_mapping.get(x.group(0), x.group(0)), cell_text)
            extracted_data.append(cell_text)
            # Replace non-numeric output with 0 for the second output
            second_output_data.extend(['0' if cell_text not in ['0', '1'] else cell_text])

    # Print the extracted data with word replacements
    print("".join(extracted_data))

    # Print the second output (identical to the first output, except non-numeric values replaced with 0)
    second_output_str = "".join(second_output_data)
    print(second_output_str)

    # Convert the binary string to its hexadecimal equivalent
    third_output_str = hex(int(second_output_str, 2))
    print("Third Output (Hexadecimal):")
    print(third_output_str[2:])  # Remove '0x' prefix
    # Print the third output without '0x' and in groups of 4 hex digits
    grouped_hex = []
    for i in range(0, len(third_output_str) - 2, 4):
        grouped_hex.append(third_output_str[i:i+4])
    print("Third Output (Grouped Hexadecimal):")
    print("".join(grouped_hex))
else:
    print("Table does not have enough rows.")
    
# sf101101011000000001000nnnnnddddd
# 01011010110000000010000000000000
# Third Output (Hexadecimal):
# 5ac02000
# Third Output (Grouped Hexadecimal):
# 0x5ac020